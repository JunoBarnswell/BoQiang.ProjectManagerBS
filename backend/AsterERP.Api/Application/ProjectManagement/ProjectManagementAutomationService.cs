using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.Workflows;
using AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementAutomationService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy,
    IWorkflowInstanceAppService workflowInstanceService,
    IApplicationDataSecretProtector secretProtector,
    IHttpClientFactory httpClientFactory,
    IBackgroundJobManager backgroundJobManager,
    IProjectManagementActivityWriter activityWriter,
    ILogger<ProjectManagementAutomationService> logger) : IProjectManagementAutomationService
{
    private const string MenuCode = "project-management";
    private const string AutomationRulesProperty = "automationRules";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProjectManagementAutomationRulesResponse> GetRulesAsync(string entityType, CancellationToken cancellationToken = default)
    {
        RequireWorkspace();
        var binding = await FindBindingAsync(NormalizeEntityType(entityType), cancellationToken);
        return MapRules(entityType, binding);
    }

    public async Task<ProjectManagementAutomationRulesResponse> SaveRuleAsync(ProjectManagementAutomationRuleUpsertRequest request, CancellationToken cancellationToken = default)
    {
        RequireWorkspace();
        ArgumentNullException.ThrowIfNull(request);
        var entityType = NormalizeEntityType(request.EntityType);
        ValidateRule(request);
        var binding = await FindBindingAsync(entityType, cancellationToken)
            ?? throw new ValidationException("项目审批未配置 BPMN 绑定");
        var root = ParseRoot(binding.BindingConfigJson);
        var rules = ReadRules(root);
        var ruleId = string.IsNullOrWhiteSpace(request.RuleId) ? Guid.NewGuid().ToString("N") : request.RuleId.Trim();
        var existing = rules.FindIndex(rule => string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal));
        var stored = new StoredRule(
            ruleId, request.Enabled, entityType, request.Trigger.Trim(), Normalize(request.Status),
            Normalize(request.AssigneeUserId), Normalize(request.MilestoneId), request.DueWithinDays,
            request.ActionType.Trim(), Normalize(request.WebhookUrl),
            request.WebhookSecret is null
                ? existing >= 0 ? rules[existing].WebhookSecretCipher : null
                : string.IsNullOrWhiteSpace(request.WebhookSecret) ? null : secretProtector.Protect(request.WebhookSecret),
            request.WebhookHeaders ?? (existing >= 0 ? rules[existing].WebhookHeaders : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
        if (existing >= 0) rules[existing] = stored; else rules.Add(stored);
        root[AutomationRulesProperty] = JsonSerializer.SerializeToNode(rules, JsonOptions);
        binding.BindingConfigJson = root.ToJsonString(JsonOptions);
        binding.UpdatedBy = User();
        binding.UpdatedTime = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Updateable(binding).UpdateColumns(item => new { item.BindingConfigJson, item.UpdatedBy, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
        return MapRules(entityType, binding);
    }

    public async Task<ProjectManagementApprovalResponse> StartApprovalAsync(string entityType, string entityId, ProjectManagementApprovalStartRequest request, CancellationToken cancellationToken = default)
    {
        RequireWorkspace();
        entityType = NormalizeEntityType(entityType);
        var target = await LoadTargetAsync(entityType, entityId, cancellationToken);
        await accessPolicy.EnsureCanManageProjectAsync(target.ProjectId, cancellationToken);
        var binding = await FindBindingAsync(entityType, cancellationToken)
            ?? throw new ValidationException("项目审批未配置 BPMN 绑定");
        if (!binding.IsEnabled) throw new ValidationException("项目审批绑定已停用");
        var idempotencyKey = Normalize(request.IdempotencyKey) ?? "manual";
        var businessKey = $"{entityType}:{target.Id}:{idempotencyKey}";
        var existing = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.MenuCode == MenuCode && item.BusinessType == entityType && item.BusinessKey == businessKey && !item.IsDeleted)
            .OrderBy(item => item.StartedAt, OrderByType.Desc).Take(1).ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            var current = existing[0];
            return new(current.ProcessInstanceId, entityType, businessKey, current.Status, current.ProcessDefinitionKey, binding.DetailRoute, true);
        }

        var variables = request.Variables is null ? new Dictionary<string, object?>() : new(request.Variables, StringComparer.OrdinalIgnoreCase);
        variables["projectManagementEntityType"] = entityType;
        variables["projectManagementEntityId"] = target.Id;
        variables["projectManagementProjectId"] = target.ProjectId;
        variables["projectManagementStatus"] = target.Status;
        variables["projectManagementVersionNo"] = target.VersionNo;
        var started = await workflowInstanceService.StartAsync(new WorkflowStartInstanceRequest(
            Tenant(), App(), MenuCode, entityType, businessKey, request.Title ?? target.Title, variables), cancellationToken);
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(Tenant(), App(), entityType, target.Id, "approval.started", "启动审批", businessKey, User(), target.ProjectId), cancellationToken);
        return new(started.ProcessInstanceId, entityType, businessKey, started.Status, started.ProcessDefinitionKey, binding.DetailRoute, false);
    }

    public async Task HandleEntityChangedAsync(string entityType, string entityId, string projectId, string? status, string? assigneeUserId, string? milestoneId, DateTime? dueDate, long versionNo, string eventType, string traceId, CancellationToken cancellationToken = default)
    {
        RequireWorkspace();
        var binding = await FindBindingAsync(NormalizeEntityType(entityType), cancellationToken);
        if (binding is null || !binding.IsEnabled) return;
        var rules = ReadRules(ParseRoot(binding.BindingConfigJson));
        foreach (var rule in rules.Where(item => item.Enabled && Matches(item, entityType, status, assigneeUserId, milestoneId, dueDate)))
        {
            if (rule.ActionType == ProjectManagementAutomationActionTypes.StartApproval)
            {
                await StartApprovalAsync(entityType, entityId, new ProjectManagementApprovalStartRequest($"{rule.RuleId}:{versionNo}", null, new Dictionary<string, object?> { ["trigger"] = eventType }), cancellationToken);
            }
            else if (rule.ActionType == ProjectManagementAutomationActionTypes.Webhook)
            {
                await EnqueueWebhookAsync(rule, entityType, entityId, projectId, status, assigneeUserId, milestoneId, dueDate, versionNo, eventType, traceId, cancellationToken);
            }
        }
    }

    public async Task<ProjectManagementAutomationDeliveryResponse> ReplayDeliveryAsync(string deliveryId, ProjectManagementAutomationReplayRequest request, CancellationToken cancellationToken = default)
    {
        RequireWorkspace();
        var operation = await LoadDeliveryAsync(deliveryId, cancellationToken) ?? throw new ValidationException("Webhook 投递不存在");
        var envelope = DeserializeEnvelope(operation.ImpactJson);
        await accessPolicy.EnsureCanManageProjectAsync(envelope.Payload.ProjectId, cancellationToken);
        var now = DateTime.UtcNow;
        await databaseAccessor.GetProjectManagementDb().Updateable<ProjectManagementOperationEntity>()
            .SetColumns(item => new ProjectManagementOperationEntity { Status = "Pending", Phase = "Queued", ErrorMessage = null, CompletedTime = null, VersionNo = item.VersionNo + 1, UpdatedBy = User(), UpdatedTime = now })
            .Where(item => item.Id == operation.Id && item.TenantId == Tenant() && item.AppCode == App() && item.Status == "Failed" && !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);
        await backgroundJobManager.EnqueueAsync(new ProjectManagementAutomationWebhookJobArgs(operation.Id, Tenant(), App(), operation.ActorUserId, envelope.Payload.TraceId));
        return MapDelivery(await LoadDeliveryAsync(operation.Id, cancellationToken) ?? operation, envelope.Payload.EventType);
    }

    public async Task ExecuteWebhookAsync(ProjectManagementAutomationWebhookJobArgs args, CancellationToken cancellationToken)
    {
        EnsureContext(args);
        var db = databaseAccessor.GetProjectManagementDb();
        var operation = await LoadDeliveryAsync(args.DeliveryId, cancellationToken);
        if (operation is null) return;
        if (operation.Status == "Succeeded") return;
        var envelope = DeserializeEnvelope(operation.ImpactJson);
        var claimed = await db.Updateable<ProjectManagementOperationEntity>()
            .SetColumns(item => new ProjectManagementOperationEntity { Status = "Running", Phase = "Sending", VersionNo = item.VersionNo + 1, UpdatedBy = User(), UpdatedTime = DateTime.UtcNow })
            .Where(item => item.Id == operation.Id && item.VersionNo == operation.VersionNo && (item.Status == "Pending" || item.Status == "Failed") && !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);
        if (claimed != 1) return;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, envelope.Rule.WebhookUrl);
            request.Content = new StringContent(envelope.BodyJson, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("X-AsterERP-Delivery-Id", operation.Id);
            request.Headers.TryAddWithoutValidation("X-AsterERP-Timestamp", envelope.Timestamp.ToString("O"));
            request.Headers.TryAddWithoutValidation("X-AsterERP-Signature", envelope.Signature);
            foreach (var header in envelope.Rule.WebhookHeaders) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            using var response = await httpClientFactory.CreateClient(ApplicationDataOutboundHttpClient.Name).SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Webhook 返回 {(int)response.StatusCode}");
            await db.Updateable<ProjectManagementOperationEntity>()
                .SetColumns(item => new ProjectManagementOperationEntity { Status = "Succeeded", Phase = "Completed", ProgressPercent = 100, CompletedTime = DateTime.UtcNow, ErrorMessage = null, VersionNo = item.VersionNo + 1, UpdatedBy = User(), UpdatedTime = DateTime.UtcNow })
                .Where(item => item.Id == operation.Id && item.Status == "Running" && !item.IsDeleted).ExecuteCommandAsync(cancellationToken);
            try { await activityWriter.AppendAsync(new ProjectManagementActivityEvent(Tenant(), App(), envelope.Payload.EntityType, envelope.Payload.EntityId, "automation.webhook.sent", "发送自动化 WebHook", operation.Id, User(), envelope.Payload.ProjectId), cancellationToken); } catch (Exception ex) { logger.LogWarning(ex, "Webhook 审计写入失败，delivery={DeliveryId}", operation.Id); }
        }
        catch (Exception exception)
        {
            await db.Updateable<ProjectManagementOperationEntity>()
                .SetColumns(item => new ProjectManagementOperationEntity { Status = "Failed", Phase = "DeadLetter", ErrorMessage = Trim(exception.Message), CompletedTime = DateTime.UtcNow, VersionNo = item.VersionNo + 1, UpdatedBy = User(), UpdatedTime = DateTime.UtcNow })
                .Where(item => item.Id == operation.Id && item.Status == "Running" && !item.IsDeleted).ExecuteCommandAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task EnqueueWebhookAsync(StoredRule rule, string entityType, string entityId, string projectId, string? status, string? assigneeUserId, string? milestoneId, DateTime? dueDate, long versionNo, string eventType, string traceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rule.WebhookUrl) || string.IsNullOrWhiteSpace(rule.WebhookSecretCipher)) return;
        var payload = new ProjectManagementAutomationWebhookPayload(eventType, rule.RuleId, entityType, entityId, projectId, status, assigneeUserId, milestoneId, versionNo, DateTime.UtcNow, traceId, new Dictionary<string, object?> { ["dueDate"] = dueDate });
        var body = JsonSerializer.Serialize(payload, JsonOptions);
        var timestamp = DateTime.UtcNow;
        var deliveryId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{Tenant()}|{App()}|{rule.RuleId}|{entityType}|{entityId}|{versionNo}|{body}"))).ToLowerInvariant();
        var db = databaseAccessor.GetProjectManagementDb();
        if (await db.Queryable<ProjectManagementOperationEntity>().AnyAsync(item => item.Id == deliveryId && !item.IsDeleted, cancellationToken)) return;
        var secret = secretProtector.Unprotect(rule.WebhookSecretCipher);
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp:O}.{body}"))).ToLowerInvariant();
        var envelope = new WebhookEnvelope(payload, rule, body, timestamp, signature);
        var operation = new ProjectManagementOperationEntity { Id = deliveryId, TenantId = Tenant(), AppCode = App(), OperationType = "automation.webhook", Status = "Pending", Phase = "Queued", ImpactJson = JsonSerializer.Serialize(envelope, JsonOptions), TraceId = traceId, ActorUserId = User(), StartedTime = DateTime.UtcNow, CreatedBy = User(), CreatedTime = DateTime.UtcNow };
        await db.Insertable(operation).ExecuteCommandAsync(cancellationToken);
        try { await backgroundJobManager.EnqueueAsync(new ProjectManagementAutomationWebhookJobArgs(deliveryId, Tenant(), App(), User(), traceId)); }
        catch (Exception exception)
        {
            await db.Updateable<ProjectManagementOperationEntity>().SetColumns(item => new ProjectManagementOperationEntity { Status = "Failed", Phase = "DeadLetter", ErrorMessage = Trim(exception.Message), CompletedTime = DateTime.UtcNow, VersionNo = item.VersionNo + 1, UpdatedBy = User(), UpdatedTime = DateTime.UtcNow }).Where(item => item.Id == deliveryId).ExecuteCommandAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<WorkflowBindingEntity?> FindBindingAsync(string entityType, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<WorkflowBindingEntity>().Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.MenuCode == MenuCode && item.BusinessType == entityType && !item.IsDeleted).OrderBy(item => item.UpdatedTime, OrderByType.Desc).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();

    private async Task<ProjectManagementOperationEntity?> LoadDeliveryAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();

    private async Task<Target> LoadTargetAsync(string entityType, string id, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        if (entityType == ProjectManagementAutomationEntityTypes.Project)
        {
            var row = await db.Queryable<ProjectManagementProjectEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken) ?? throw new ValidationException("项目不存在");
            return new(row.Id, row.Id, row.ProjectName, row.Status, null, null, row.DueDate, row.VersionNo);
        }
        if (entityType == ProjectManagementAutomationEntityTypes.Task)
        {
            var row = await db.Queryable<ProjectManagementTaskEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken) ?? throw new ValidationException("任务不存在");
            return new(row.Id, row.ProjectId, row.Title, row.Status, row.AssigneeUserId, row.MilestoneId, row.DueDate, row.VersionNo);
        }
        var milestone = await db.Queryable<ProjectManagementMilestoneEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken) ?? throw new ValidationException("里程碑不存在");
        return new(milestone.Id, milestone.ProjectId, milestone.MilestoneName, milestone.Status, milestone.OwnerUserId, milestone.Id, milestone.DueDate, milestone.VersionNo);
    }

    private static bool Matches(StoredRule rule, string entityType, string? status, string? assignee, string? milestone, DateTime? dueDate) =>
        string.Equals(rule.EntityType, entityType, StringComparison.OrdinalIgnoreCase) && rule.Trigger switch
        {
            ProjectManagementAutomationTriggerTypes.StatusChanged => string.IsNullOrWhiteSpace(rule.Status) || string.Equals(rule.Status, status, StringComparison.Ordinal),
            ProjectManagementAutomationTriggerTypes.AssigneeChanged => string.IsNullOrWhiteSpace(rule.AssigneeUserId) || string.Equals(rule.AssigneeUserId, assignee, StringComparison.Ordinal),
            ProjectManagementAutomationTriggerTypes.MilestoneChanged => string.IsNullOrWhiteSpace(rule.MilestoneId) || string.Equals(rule.MilestoneId, milestone, StringComparison.Ordinal),
            ProjectManagementAutomationTriggerTypes.DueDate => dueDate.HasValue && (!rule.DueWithinDays.HasValue || dueDate.Value.Date <= DateTime.UtcNow.Date.AddDays(rule.DueWithinDays.Value)),
            _ => false
        };

    private static JsonObject ParseRoot(string? json) => string.IsNullOrWhiteSpace(json) ? new JsonObject() : JsonNode.Parse(json) as JsonObject ?? new JsonObject();
    private static List<StoredRule> ReadRules(JsonObject root) => root[AutomationRulesProperty]?.Deserialize<List<StoredRule>>(JsonOptions) ?? [];
    private static ProjectManagementAutomationRulesResponse MapRules(string entityType, WorkflowBindingEntity? binding)
    {
        List<StoredRule> rules = binding is null ? [] : ReadRules(ParseRoot(binding.BindingConfigJson));
        return new(entityType, binding is not null, binding?.ProcessDefinitionKey, rules.Select(rule => new ProjectManagementAutomationRuleResponse(rule.RuleId, rule.Enabled, rule.EntityType, rule.Trigger, rule.Status, rule.AssigneeUserId, rule.MilestoneId, rule.DueWithinDays, rule.ActionType, rule.WebhookUrl, !string.IsNullOrWhiteSpace(rule.WebhookSecretCipher), rule.WebhookHeaders)).ToList());
    }
    private static string NormalizeEntityType(string value) => ProjectManagementAutomationEntityTypes.IsSupported(value.Trim()) ? value.Trim() : throw new ValidationException("自动化对象类型不受支持");
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void ValidateRule(ProjectManagementAutomationRuleUpsertRequest request)
    {
        if (!EnumLike(request.Trigger, ProjectManagementAutomationTriggerTypes.StatusChanged, ProjectManagementAutomationTriggerTypes.DueDate, ProjectManagementAutomationTriggerTypes.AssigneeChanged, ProjectManagementAutomationTriggerTypes.MilestoneChanged)) throw new ValidationException("自动化触发器不受支持");
        if (!EnumLike(request.ActionType, ProjectManagementAutomationActionTypes.StartApproval, ProjectManagementAutomationActionTypes.Webhook)) throw new ValidationException("自动化动作不受支持");
        if (request.DueWithinDays is < 0 or > 365) throw new ValidationException("到期窗口必须在 0 到 365 天之间");
        if (request.ActionType == ProjectManagementAutomationActionTypes.Webhook)
        {
            if (!Uri.TryCreate(request.WebhookUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrWhiteSpace(uri.UserInfo)) throw new ValidationException("Webhook 地址无效");
            if (request.WebhookSecret is not null && request.WebhookSecret.Length is < 16 or > 512) throw new ValidationException("Webhook 密钥长度必须在 16 到 512 个字符之间");
        }
    }
    private static bool EnumLike(string? actual, params string[] allowed) => allowed.Contains(actual?.Trim(), StringComparer.OrdinalIgnoreCase);
    private void RequireWorkspace() => ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private void EnsureContext(ProjectManagementAutomationWebhookJobArgs args) { if (args.TenantId != Tenant() || !string.Equals(args.AppCode, App(), StringComparison.OrdinalIgnoreCase) || args.ActorUserId != User()) throw new InvalidOperationException("自动化作业上下文不匹配"); }
    private static string Trim(string value) => value.Length <= 2_000 ? value : value[..2_000];
    private static ProjectManagementAutomationDeliveryResponse MapDelivery(ProjectManagementOperationEntity entity, string eventType) => new(entity.Id, entity.Status, eventType, entity.ErrorMessage, entity.StartedTime, entity.CompletedTime, entity.ProgressPercent);
    private static WebhookEnvelope DeserializeEnvelope(string json) => JsonSerializer.Deserialize<WebhookEnvelope>(json, JsonOptions) ?? throw new ValidationException("Webhook 投递载荷已损坏");
    private sealed record Target(string Id, string ProjectId, string Title, string Status, string? AssigneeUserId, string? MilestoneId, DateTime? DueDate, long VersionNo);
    private sealed record StoredRule(string RuleId, bool Enabled, string EntityType, string Trigger, string? Status, string? AssigneeUserId, string? MilestoneId, int? DueWithinDays, string ActionType, string? WebhookUrl, string? WebhookSecretCipher, Dictionary<string, string> WebhookHeaders);
    private sealed record WebhookEnvelope(ProjectManagementAutomationWebhookPayload Payload, StoredRule Rule, string BodyJson, DateTime Timestamp, string Signature);
}

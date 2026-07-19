using System.Text;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 项目成员可见的业务时间线。平台安全/操作审计由独立的审计基础设施负责，不能把原始敏感值写入此处。
/// </summary>
public sealed class ProjectManagementActivityService(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser, IProjectManagementWebhookService? webhookService = null) : IProjectManagementActivityService
{
    private const int MaxPayloadBytes = 64 * 1024;
    private const int MaxFieldChanges = 64;
    private const int MaxBatchDetails = 200;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default)
    {
        var projectId = Required(activity.ProjectId, "活动必须绑定项目");
        var payload = CreatePayload(activity);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        if (Encoding.UTF8.GetByteCount(payloadJson) > MaxPayloadBytes)
            throw new ValidationException("活动明细超过 64KB 限制");

        var db = databaseAccessor.GetCurrentDb();
        await db.Insertable(new ProjectManagementActivityEntity
        {
            TenantId = RequireTenant(),
            AppCode = RequireApp(),
            ProjectId = projectId,
            AggregateType = Required(activity.AggregateType),
            AggregateId = Required(activity.AggregateId),
            ActivityType = Required(activity.ActivityType),
            Summary = NormalizeOptional(activity.Summary, 1_000, "活动摘要"),
            TraceId = Required(activity.TraceId),
            ActorUserId = Required(activity.ActorUserId),
            CreatedBy = Required(activity.ActorUserId),
            CreatedTime = activity.OccurredAt ?? DateTime.UtcNow,
            Remark = payloadJson
        }).ExecuteCommandAsync(cancellationToken);
        if (webhookService is not null)
            await webhookService.PublishActivityAsync(activity, cancellationToken);
    }

    public async Task<GridPageResult<ProjectManagementActivityResponse>> QueryAsync(
        string projectId,
        ProjectManagementActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        RequireTenant();
        RequireApp();
        projectId = Required(projectId, "项目标识不能为空");
        ValidateQuery(query);

        // QueryFilter 由 ProjectManagementDataPermissionFilterRegistrar 注册：租户、应用工作区与项目成员范围均在数据库端约束。
        // 故意不排除 IsDeleted：即使聚合根或活动行被软删除，时间线仍须保留可审计证据。
        var activityQuery = databaseAccessor.GetCurrentDb().Queryable<ProjectManagementActivityEntity>()
            .Where(item => item.ProjectId == projectId);
        return await QueryPageAsync(activityQuery, query, null, cancellationToken);
    }

    public async Task<GridPageResult<ProjectManagementActivityResponse>> QueryTaskAsync(
        string taskId,
        ProjectManagementActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var appCode = RequireApp();
        taskId = Required(taskId, "任务标识不能为空");
        ValidateQuery(query);

        var db = databaseAccessor.GetCurrentDb();
        var task = (await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.Id == taskId && item.TenantId == tenantId && item.AppCode == appCode)
            .Take(1)
            .ToListAsync(cancellationToken)).FirstOrDefault();
        if (task is null)
            throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);

        // 任务活动统一复用 pm_activities：任务自身事件直接绑定 Task，协作事件通过既有关系表回溯到任务。
        var activityQuery = db.Queryable<ProjectManagementActivityEntity>()
            .Where(item => item.ProjectId == task.ProjectId &&
                ((item.AggregateType == "Task" && item.AggregateId == taskId) ||
                 (item.AggregateType == "TaskComment" && SqlFunc.Subqueryable<ProjectManagementTaskCommentEntity>()
                    .Where(comment => comment.Id == item.AggregateId && comment.TenantId == tenantId && comment.AppCode == appCode && comment.TaskId == taskId).Any()) ||
                 (item.AggregateType == "TaskAttachment" && SqlFunc.Subqueryable<ProjectManagementTaskAttachmentEntity>()
                    .Where(attachment => attachment.Id == item.AggregateId && attachment.TenantId == tenantId && attachment.AppCode == appCode && attachment.TaskId == taskId).Any()) ||
                 (item.AggregateType == "TaskParticipant" && SqlFunc.Subqueryable<ProjectManagementTaskParticipantEntity>()
                    .Where(participant => participant.Id == item.AggregateId && participant.TenantId == tenantId && participant.AppCode == appCode && participant.TaskId == taskId).Any()) ||
                 (item.AggregateType == "TaskDependency" && SqlFunc.Subqueryable<ProjectManagementTaskDependencyEntity>()
                    .Where(dependency => dependency.Id == item.AggregateId && dependency.TenantId == tenantId && dependency.AppCode == appCode &&
                        (dependency.PredecessorTaskId == taskId || dependency.SuccessorTaskId == taskId)).Any()) ||
                 (item.AggregateType == "ImConversationLink" && SqlFunc.Subqueryable<ProjectManagementImConversationLinkEntity>()
                    .Where(link => link.Id == item.AggregateId && link.TenantId == tenantId && link.AppCode == appCode && link.TaskId == taskId).Any())));
        return await QueryPageAsync(activityQuery, query, taskId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectManagementActivityResponse>> QueryAsync(
        string projectId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var page = await QueryAsync(projectId, new ProjectManagementActivityQuery(PageSize: Math.Clamp(limit, 1, 200)), cancellationToken);
        return page.Items;
    }

    private static ProjectManagementActivityPayload CreatePayload(ProjectManagementActivityEvent activity)
    {
        var changes = NormalizeChanges(activity.FieldChanges, MaxFieldChanges, "字段差异");
        var batch = NormalizeBatch(activity.Batch);
        return new ProjectManagementActivityPayload(
            NormalizeOptional(activity.Source, 64, "活动来源") ?? "Business",
            changes,
            batch);
    }

    private static ProjectManagementActivityBatch? NormalizeBatch(ProjectManagementActivityBatch? batch)
    {
        if (batch is null) return null;
        if (batch.TotalCount < 0 || batch.SuccessCount < 0 || batch.FailureCount < 0 || batch.SuccessCount + batch.FailureCount > batch.TotalCount)
            throw new ValidationException("批量活动统计无效");
        var details = batch.Details ?? [];
        if (details.Count > MaxBatchDetails) throw new ValidationException($"批量活动明细不能超过 {MaxBatchDetails} 项");
        return batch with
        {
            OperationId = Required(batch.OperationId),
            Details = details.Select(item => new ProjectManagementActivityBatchItem(
                Required(item.AggregateType),
                Required(item.AggregateId),
                NormalizeOptional(item.Summary, 1_000, "批量活动摘要"),
                NormalizeChanges(item.FieldChanges, MaxFieldChanges, "批量字段差异"))).ToList()
        };
    }

    private static IReadOnlyList<ProjectManagementActivityFieldChange> NormalizeChanges(
        IReadOnlyList<ProjectManagementActivityFieldChange>? changes,
        int maximum,
        string name)
    {
        var source = changes ?? [];
        if (source.Count > maximum) throw new ValidationException($"{name}不能超过 {maximum} 项");
        return source.Select(change =>
        {
            var field = Required(change.Field);
            var sensitive = change.IsSensitive || IsSensitiveField(field);
            return change with
            {
                Field = field,
                DisplayName = NormalizeOptional(change.DisplayName, 128, "字段显示名"),
                Before = sensitive ? Mask(change.Before) : NormalizeOptional(change.Before, 4_000, "字段旧值"),
                After = sensitive ? Mask(change.After) : NormalizeOptional(change.After, 4_000, "字段新值"),
                IsSensitive = sensitive
            };
        }).ToList();
    }

    private async Task<GridPageResult<ProjectManagementActivityResponse>> QueryPageAsync(
        ISugarQueryable<ProjectManagementActivityEntity> activityQuery,
        ProjectManagementActivityQuery query,
        string? preferredTaskId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.AggregateType))
            activityQuery = activityQuery.Where(item => item.AggregateType == query.AggregateType.Trim());
        if (!string.IsNullOrWhiteSpace(query.AggregateId))
            activityQuery = activityQuery.Where(item => item.AggregateId == query.AggregateId.Trim());
        if (!string.IsNullOrWhiteSpace(query.ActivityType))
            activityQuery = activityQuery.Where(item => item.ActivityType == query.ActivityType.Trim());
        if (!string.IsNullOrWhiteSpace(query.ActorUserId))
            activityQuery = activityQuery.Where(item => item.ActorUserId == query.ActorUserId.Trim());
        if (query.From.HasValue)
            activityQuery = activityQuery.Where(item => item.CreatedTime >= query.From.Value);
        if (query.To.HasValue)
            activityQuery = activityQuery.Where(item => item.CreatedTime <= query.To.Value);

        var total = new RefAsync<int>();
        var rows = await activityQuery
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .OrderBy(item => item.Id, OrderByType.Desc)
            .ToPageListAsync(Math.Max(1, query.PageIndex), Math.Clamp(query.PageSize, 1, 200), total, cancellationToken);
        return new GridPageResult<ProjectManagementActivityResponse>
        {
            Total = total.Value,
            Items = (await MapManyAsync(rows, preferredTaskId, cancellationToken)).ToList()
        };
    }

    private async Task<IReadOnlyList<ProjectManagementActivityResponse>> MapManyAsync(
        IReadOnlyList<ProjectManagementActivityEntity> entities,
        string? preferredTaskId,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return [];
        var db = databaseAccessor.GetCurrentDb();
        var tenantId = RequireTenant();
        var appCode = RequireApp();
        var targets = new Dictionary<string, ActivityTarget>(StringComparer.Ordinal);
        var taskIds = entities.Where(item => item.AggregateType == "Task")
            .Select(item => item.AggregateId)
            .Concat(entities.Where(item => item.AggregateType == "TaskComment").Select(item => item.AggregateId))
            .ToList();

        var commentIds = entities.Where(item => item.AggregateType == "TaskComment").Select(item => item.AggregateId).ToList();
        var attachmentIds = entities.Where(item => item.AggregateType == "TaskAttachment").Select(item => item.AggregateId).ToList();
        var participantIds = entities.Where(item => item.AggregateType == "TaskParticipant").Select(item => item.AggregateId).ToList();
        var dependencyIds = entities.Where(item => item.AggregateType == "TaskDependency").Select(item => item.AggregateId).ToList();
        var conversationIds = entities.Where(item => item.AggregateType == "ImConversationLink").Select(item => item.AggregateId).ToList();

        var tasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && taskIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var comments = commentIds.Count == 0 ? [] : await db.Queryable<ProjectManagementTaskCommentEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && commentIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var attachments = attachmentIds.Count == 0 ? [] : await db.Queryable<ProjectManagementTaskAttachmentEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && attachmentIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var participants = participantIds.Count == 0 ? [] : await db.Queryable<ProjectManagementTaskParticipantEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && participantIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var dependencies = dependencyIds.Count == 0 ? [] : await db.Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && dependencyIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var conversations = conversationIds.Count == 0 ? [] : await db.Queryable<ProjectManagementImConversationLinkEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && conversationIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        taskIds = taskIds.Concat(comments.Select(item => item.TaskId))
            .Concat(attachments.Select(item => item.TaskId))
            .Concat(participants.Select(item => item.TaskId))
            .Concat(dependencies.SelectMany(item => new[] { item.PredecessorTaskId, item.SuccessorTaskId }))
            .Concat(conversations.Where(item => item.TaskId is not null).Select(item => item.TaskId!))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var relatedTasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && taskIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var taskDeleted = relatedTasks.ToDictionary(item => item.Id, item => item.IsDeleted, StringComparer.Ordinal);
        foreach (var task in tasks) targets[TargetKey("Task", task.Id)] = new(task.Id, task.IsDeleted);
        foreach (var item in comments) targets[TargetKey("TaskComment", item.Id)] = new(item.TaskId, item.IsDeleted || taskDeleted.GetValueOrDefault(item.TaskId));
        foreach (var item in attachments) targets[TargetKey("TaskAttachment", item.Id)] = new(item.TaskId, item.IsDeleted || taskDeleted.GetValueOrDefault(item.TaskId));
        foreach (var item in participants) targets[TargetKey("TaskParticipant", item.Id)] = new(item.TaskId, item.IsDeleted || taskDeleted.GetValueOrDefault(item.TaskId));
        foreach (var item in dependencies)
        {
            var targetTaskId = preferredTaskId is not null && (item.PredecessorTaskId == preferredTaskId || item.SuccessorTaskId == preferredTaskId)
                ? preferredTaskId
                : item.SuccessorTaskId;
            targets[TargetKey("TaskDependency", item.Id)] = new(targetTaskId, item.IsDeleted || taskDeleted.GetValueOrDefault(item.PredecessorTaskId) || taskDeleted.GetValueOrDefault(item.SuccessorTaskId));
        }
        foreach (var item in conversations.Where(item => item.TaskId is not null))
            targets[TargetKey("ImConversationLink", item.Id)] = new(item.TaskId!, item.IsDeleted || taskDeleted.GetValueOrDefault(item.TaskId!));

        return entities.Select(entity =>
        {
            targets.TryGetValue(TargetKey(entity.AggregateType, entity.AggregateId), out var target);
            return Map(entity, target);
        }).ToList();
    }

    private static ProjectManagementActivityResponse Map(ProjectManagementActivityEntity entity, ActivityTarget? target = null)
    {
        var payload = DeserializePayload(entity.Remark);
        var targetRoute = target is null || target.IsDeleted
            ? null
            : $"/projects/{Uri.EscapeDataString(entity.ProjectId)}/tasks?selectedTaskId={Uri.EscapeDataString(target.TaskId)}";
        return new ProjectManagementActivityResponse(
            entity.Id,
            entity.ProjectId,
            entity.AggregateType,
            entity.AggregateId,
            entity.ActivityType,
            entity.Summary,
            entity.TraceId,
            entity.ActorUserId,
            entity.CreatedTime,
            payload?.Source ?? "Business",
            payload?.FieldChanges ?? [],
            payload?.Batch,
            targetRoute,
            target?.IsDeleted ?? false);
    }

    private static string TargetKey(string aggregateType, string aggregateId) => $"{aggregateType}\u001f{aggregateId}";

    private sealed record ActivityTarget(string TaskId, bool IsDeleted);

    private static ProjectManagementActivityPayload? DeserializePayload(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return JsonSerializer.Deserialize<ProjectManagementActivityPayload>(value, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static void ValidateQuery(ProjectManagementActivityQuery query)
    {
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            throw new ValidationException("活动时间范围无效");
        if (query.AggregateType?.Length > 128) throw new ValidationException("聚合类型不能超过 128 个字符");
        if (query.AggregateId?.Length > 128) throw new ValidationException("聚合标识不能超过 128 个字符");
        if (query.ActivityType?.Length > 128) throw new ValidationException("活动类型不能超过 128 个字符");
        if (query.ActorUserId?.Length > 128) throw new ValidationException("操作者标识不能超过 128 个字符");
    }

    private string RequireTenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);

    private string RequireApp() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);

    private static string Required(string? value, string message = "活动字段不能为空") => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();

    private static string? NormalizeOptional(string? value, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximum) throw new ValidationException($"{name}不能超过 {maximum} 个字符");
        return normalized;
    }

    private static bool IsSensitiveField(string field) =>
        field.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("privatekey", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("密码", StringComparison.Ordinal) ||
        field.Contains("密钥", StringComparison.Ordinal) ||
        field.Contains("令牌", StringComparison.Ordinal);

    private static string? Mask(string? value) => value is null ? null : "[已脱敏]";
}

using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using System.Text.Json;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseChatflowService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    IAiSecretProtector secretProtector,
    FlowisePermissionGuard permissionGuard,
    FlowiseFlowDataValidator flowDataValidator,
    IFlowiseScheduleScheduler scheduleScheduler) : IFlowiseChatflowService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 500;
    private const string OfficialDemoTemplateKey = "\"templateKey\":\"supervisor-workers-demo\"";

    public async Task<GridPageResult<FlowiseChatflowDto>> GetPageAsync(FlowiseChatflowQuery query, CancellationToken cancellationToken)
    {
        var type = NormalizeType(query.Type);
        EnsureView(type);
        var dbQuery = db.Queryable<FlowiseChatFlowEntity>()
            .Where(item => !item.IsDeleted && item.Type == type);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.Name.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            var workspaceId = query.WorkspaceId.Trim();
            dbQuery = dbQuery.Where(item => item.WorkspaceId == workspaceId);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            dbQuery = dbQuery.Where(item => item.Category == category);
        }

        if (query.Deployed.HasValue)
        {
            dbQuery = dbQuery.Where(item => item.Deployed == query.Deployed.Value);
        }

        var total = new RefAsync<int>();
        if (type == FlowiseChatflowTypes.Agentflow)
        {
            dbQuery = dbQuery.OrderBy(item => item.MetadataJson.Contains(OfficialDemoTemplateKey), OrderByType.Desc);
        }

        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePageIndex(query.PageIndex), NormalizePageSize(query.PageSize), total);
        return new GridPageResult<FlowiseChatflowDto>
        {
            Total = total.Value,
            Items = rows.Select(Map).ToList()
        };
    }

    public async Task<FlowiseChatflowDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken);
        EnsureView(entity.Type);
        return Map(entity);
    }

    public async Task<FlowiseChatflowDto> CreateAsync(FlowiseChatflowUpsertRequest request, CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        EnsureEdit(normalized.Type);
        var workspace = workspaceContext.Resolve();
        await EnsureWorkspaceExistsAsync(normalized.WorkspaceId, cancellationToken);
        var entity = new FlowiseChatFlowEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId
        };
        Apply(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await SyncScheduleRecordAsync(entity, cancellationToken);
        await WriteAuditAsync("chatflow.created", entity, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseChatflowDto> UpdateAsync(string id, FlowiseChatflowUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken);
        var normalized = NormalizeRequest(request);
        if (!string.Equals(entity.Type, normalized.Type, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Flowise ChatFlow type 不允许在更新时改变", ErrorCodes.ParameterInvalid);
        }

        EnsureEdit(entity.Type);
        await EnsureWorkspaceExistsAsync(normalized.WorkspaceId, cancellationToken);
        Apply(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await SyncScheduleRecordAsync(entity, cancellationToken);
        await WriteAuditAsync("chatflow.updated", entity, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseChatflowDto> UpdateConfigurationAsync(string id, FlowiseChatflowConfigurationRequest request, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken);
        EnsureConfig(entity.Type);
        ApplyConfiguration(entity, request);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("chatflow.configuration.updated", entity, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseChatflowDto> UpdateDomainsAsync(string id, FlowiseChatflowDomainsRequest request, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken);
        EnsureDomains(entity.Type);
        entity.ChatbotConfig = flowDataValidator.NormalizeJsonObject(request.ChatbotConfig);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("chatflow.domains.updated", entity, cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken);
        EnsureDelete(entity.Type);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await DeleteScheduleRecordAsync(entity, cancellationToken);
        await WriteAuditAsync("chatflow.deleted", entity, cancellationToken);
        return true;
    }

    public Task<FlowiseCanvasValidationResultDto> ValidateFlowDataAsync(string flowData, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAnyView();
        return Task.FromResult(flowDataValidator.Validate(flowData));
    }

    public async Task<FlowiseScheduleStatusDto> GetScheduleStatusAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken);
        EnsureView(entity.Type);
        var definition = ResolveScheduleDefinition(entity.FlowData);
        var record = await ApplyScheduleRecordAsync(entity, cancellationToken);
        if (record is null)
        {
            return definition.ToStatus(false);
        }

        return MapScheduleStatus(record);
    }

    public async Task<GridPageResult<FlowiseScheduleTriggerLogDto>> GetScheduleTriggerLogsAsync(string id, FlowiseScheduleLogQuery query, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken);
        EnsureView(entity.Type);
        var dbQuery = db.Queryable<FlowiseScheduleTriggerLogEntity>()
            .Where(item => !item.IsDeleted && item.TargetId == entity.Id);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePageIndex(query.PageIndex), NormalizePageSize(query.PageSize), total);
        return new GridPageResult<FlowiseScheduleTriggerLogDto>
        {
            Total = total.Value,
            Items = rows.Select(MapScheduleLog).ToList()
        };
    }

    private FlowiseChatflowUpsertRequest NormalizeRequest(FlowiseChatflowUpsertRequest request)
    {
        var name = string.IsNullOrWhiteSpace(request.Name)
            ? throw new ValidationException("Flowise ChatFlow name 不能为空", ErrorCodes.ParameterInvalid)
            : request.Name.Trim();
        var type = NormalizeType(request.Type);
        var validation = flowDataValidator.Validate(request.FlowData);
        if (!validation.Valid)
        {
            var first = validation.Issues.First(item => item.Severity == "error");
            throw new ValidationException(first.Message, ErrorCodes.ParameterInvalid);
        }

        return new FlowiseChatflowUpsertRequest
        {
            Name = name,
            FlowData = flowDataValidator.Normalize(request.FlowData),
            Type = type,
            Deployed = request.Deployed,
            IsPublic = request.IsPublic,
            Apikeyid = NormalizeOptional(request.Apikeyid),
            Category = NormalizeOptional(request.Category),
            MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson)
                ? null
                : flowDataValidator.NormalizeJsonObject(request.MetadataJson),
            WorkspaceId = NormalizeOptional(request.WorkspaceId),
            ChatbotConfig = flowDataValidator.NormalizeJsonObject(request.ChatbotConfig),
            ApiConfig = flowDataValidator.NormalizeJsonObject(request.ApiConfig),
            Analytic = flowDataValidator.NormalizeJsonObject(request.Analytic),
            SpeechToText = flowDataValidator.NormalizeJsonObject(request.SpeechToText),
            TextToSpeech = flowDataValidator.NormalizeJsonObject(request.TextToSpeech),
            FollowUpPrompts = flowDataValidator.NormalizeJsonObject(request.FollowUpPrompts),
            McpServerConfig = flowDataValidator.NormalizeJsonObject(request.McpServerConfig),
            WebhookSecret = request.WebhookSecret
        };
    }

    private void Apply(FlowiseChatFlowEntity entity, FlowiseChatflowUpsertRequest request)
    {
        entity.Name = request.Name;
        entity.FlowData = request.FlowData;
        entity.Type = request.Type;
        entity.Deployed = request.Deployed;
        entity.IsPublic = request.IsPublic;
        entity.Apikeyid = request.Apikeyid;
        entity.Category = request.Category;
        if (!string.IsNullOrWhiteSpace(request.MetadataJson))
        {
            entity.MetadataJson = request.MetadataJson;
        }
        else if (string.IsNullOrWhiteSpace(entity.MetadataJson))
        {
            entity.MetadataJson = "{}";
        }
        entity.WorkspaceId = request.WorkspaceId;
        entity.ChatbotConfig = request.ChatbotConfig ?? "{}";
        entity.ApiConfig = request.ApiConfig ?? "{}";
        entity.Analytic = request.Analytic ?? "{}";
        entity.SpeechToText = request.SpeechToText ?? "{}";
        entity.TextToSpeech = request.TextToSpeech ?? "{}";
        entity.FollowUpPrompts = request.FollowUpPrompts ?? "{}";
        entity.McpServerConfig = request.McpServerConfig ?? "{}";
        if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
        {
            entity.WebhookSecretCipherText = secretProtector.Protect(request.WebhookSecret.Trim());
            entity.WebhookSecretConfigured = true;
        }
    }

    private void ApplyConfiguration(FlowiseChatFlowEntity entity, FlowiseChatflowConfigurationRequest request)
    {
        entity.ChatbotConfig = flowDataValidator.NormalizeJsonObject(request.ChatbotConfig);
        entity.ApiConfig = flowDataValidator.NormalizeJsonObject(request.ApiConfig);
        entity.Analytic = flowDataValidator.NormalizeJsonObject(request.Analytic);
        entity.SpeechToText = flowDataValidator.NormalizeJsonObject(request.SpeechToText);
        entity.TextToSpeech = flowDataValidator.NormalizeJsonObject(request.TextToSpeech);
        entity.FollowUpPrompts = flowDataValidator.NormalizeJsonObject(request.FollowUpPrompts);
        entity.McpServerConfig = flowDataValidator.NormalizeJsonObject(request.McpServerConfig);
        if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
        {
            entity.WebhookSecretCipherText = secretProtector.Protect(request.WebhookSecret.Trim());
            entity.WebhookSecretConfigured = true;
        }
    }

    private async Task<FlowiseChatFlowEntity> LoadAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ValidationException("缺少 Flowise ChatFlow Id", ErrorCodes.ParameterInvalid);
        }

        return await db.Queryable<FlowiseChatFlowEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise ChatFlow 不存在", ErrorCodes.ParameterInvalid);
    }

    private async Task EnsureWorkspaceExistsAsync(string? workspaceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return;
        }

        var exists = await db.Queryable<FlowiseWorkspaceEntity>()
            .AnyAsync(item => !item.IsDeleted && item.Id == workspaceId.Trim(), cancellationToken);
        if (!exists)
        {
            throw new ValidationException("Flowise 工作区不存在", ErrorCodes.ParameterInvalid);
        }
    }

    private async Task WriteAuditAsync(string eventType, FlowiseChatFlowEntity entity, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        await db.Insertable(new FlowiseAuditLogEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = entity.WorkspaceId,
            EventType = eventType,
            ResourceType = entity.Type,
            ResourceId = entity.Id,
            DetailJson = JsonSerializer.Serialize(new { entity.Name, entity.Type })
        }).ExecuteCommandAsync(cancellationToken);
    }

    private void EnsureView(string type)
    {
        if (type == FlowiseChatflowTypes.Agentflow)
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
    }

    private void EnsureEdit(string type)
    {
        if (type == FlowiseChatflowTypes.Agentflow)
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
    }

    private void EnsureConfig(string type)
    {
        if (type == FlowiseChatflowTypes.Agentflow)
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsConfig, PermissionCodes.FlowiseAgentflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsConfig, PermissionCodes.FlowiseChatflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
    }

    private void EnsureDomains(string type)
    {
        if (type == FlowiseChatflowTypes.Agentflow)
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsDomains, PermissionCodes.FlowiseAgentflowsConfig, PermissionCodes.FlowiseAgentflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsDomains, PermissionCodes.FlowiseChatflowsConfig, PermissionCodes.FlowiseChatflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
    }

    private void EnsureDelete(string type)
    {
        if (type == FlowiseChatflowTypes.Agentflow)
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsDelete, PermissionCodes.FlowiseAgentflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsDelete, PermissionCodes.FlowiseChatflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
    }

    private static FlowiseChatflowDto Map(FlowiseChatFlowEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        FlowData = entity.FlowData,
        Type = entity.Type,
        Deployed = entity.Deployed,
        IsPublic = entity.IsPublic,
        Apikeyid = entity.Apikeyid,
        Category = entity.Category,
        MetadataJson = entity.MetadataJson,
        WorkspaceId = entity.WorkspaceId,
        ChatbotConfig = entity.ChatbotConfig,
        ApiConfig = entity.ApiConfig,
        Analytic = entity.Analytic,
        SpeechToText = entity.SpeechToText,
        TextToSpeech = entity.TextToSpeech,
        FollowUpPrompts = entity.FollowUpPrompts,
        McpServerConfig = entity.McpServerConfig,
        WebhookSecretConfigured = entity.WebhookSecretConfigured,
        CreatedDate = entity.CreatedTime,
        UpdatedDate = entity.UpdatedTime
    };

    private static FlowiseScheduleTriggerLogDto MapScheduleLog(FlowiseScheduleTriggerLogEntity entity) => new()
    {
        CompletedAt = entity.Status is "SUCCEEDED" or "FAILED" or "SKIPPED" ? entity.UpdatedTime ?? entity.CreatedTime : null,
        Error = entity.Error,
        ExecutionId = entity.ExecutionId,
        Id = entity.Id,
        OutputJson = "{}",
        ScheduledAt = entity.ScheduledAt,
        StartedAt = entity.CreatedTime,
        Status = entity.Status
    };

    private static FlowiseScheduleStatusDto MapScheduleStatus(FlowiseScheduleRecordEntity entity) => new()
    {
        CronExpression = entity.CronExpression,
        DefaultFormJson = entity.DefaultForm,
        DefaultInput = entity.DefaultInput,
        Enabled = entity.Enabled,
        EndDate = entity.EndDate,
        IsScheduled = !entity.IsDeleted,
        LastRunAt = entity.LastRunAt,
        NextRunAt = entity.NextRunAt,
        ScheduleInputMode = entity.ScheduleInputMode,
        Timezone = entity.Timezone
    };

    private async Task<FlowiseScheduleRecordEntity?> EnsureScheduleRecordCurrentAsync(
        FlowiseChatFlowEntity entity,
        FlowiseScheduleDefinition definition,
        CancellationToken cancellationToken)
    {
        var record = await LoadScheduleRecordAsync(entity, cancellationToken);
        if (!definition.IsScheduled)
        {
            if (record is not null)
            {
                record.IsDeleted = true;
                record.DeletedTime = DateTime.UtcNow;
                record.UpdatedTime = DateTime.UtcNow;
                await db.Updateable(record).ExecuteCommandAsync(cancellationToken);
            }

            return null;
        }

        if (record is null)
        {
            record = CreateScheduleRecord(entity, definition);
            await db.Insertable(record).ExecuteCommandAsync(cancellationToken);
            return record;
        }

        ApplyScheduleDefinition(record, entity, definition);
        record.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(record).ExecuteCommandAsync(cancellationToken);
        return record;
    }

    private async Task SyncScheduleRecordAsync(FlowiseChatFlowEntity entity, CancellationToken cancellationToken) =>
        await ApplyScheduleRecordAsync(entity, cancellationToken);

    private async Task<FlowiseScheduleRecordEntity?> ApplyScheduleRecordAsync(
        FlowiseChatFlowEntity entity,
        CancellationToken cancellationToken)
    {
        var record = await EnsureScheduleRecordCurrentAsync(entity, ResolveScheduleDefinition(entity.FlowData), cancellationToken);
        var schedulerRecord = record ?? await db.Queryable<FlowiseScheduleRecordEntity>()
            .Where(item => item.TargetId == entity.Id && item.TriggerType == "AGENTFLOW")
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        await scheduleScheduler.ApplyAsync(schedulerRecord, cancellationToken);
        return record;
    }

    private async Task DeleteScheduleRecordAsync(FlowiseChatFlowEntity entity, CancellationToken cancellationToken)
    {
        var record = await LoadScheduleRecordAsync(entity, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.IsDeleted = true;
        record.DeletedTime = DateTime.UtcNow;
        record.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(record).ExecuteCommandAsync(cancellationToken);
        await scheduleScheduler.ApplyAsync(record, cancellationToken);
    }

    private async Task<FlowiseScheduleRecordEntity?> LoadScheduleRecordAsync(FlowiseChatFlowEntity entity, CancellationToken cancellationToken) =>
        await db.Queryable<FlowiseScheduleRecordEntity>()
            .Where(item => !item.IsDeleted && item.TriggerType == "AGENTFLOW" && item.TargetId == entity.Id && item.WorkspaceId == entity.WorkspaceId)
            .FirstAsync(cancellationToken);

    private FlowiseScheduleRecordEntity CreateScheduleRecord(FlowiseChatFlowEntity entity, FlowiseScheduleDefinition definition)
    {
        var workspace = workspaceContext.Resolve();
        var record = new FlowiseScheduleRecordEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId
        };
        ApplyScheduleDefinition(record, entity, definition);
        return record;
    }

    private static void ApplyScheduleDefinition(
        FlowiseScheduleRecordEntity record,
        FlowiseChatFlowEntity entity,
        FlowiseScheduleDefinition definition)
    {
        record.WorkspaceId = entity.WorkspaceId;
        record.TriggerType = "AGENTFLOW";
        record.TargetId = entity.Id;
        record.NodeId = definition.NodeId;
        record.CronExpression = definition.CronExpression ?? "* * * * *";
        record.Timezone = definition.Timezone ?? "UTC";
        record.Enabled = definition.IsScheduled;
        record.ScheduleInputMode = definition.ScheduleInputMode;
        record.DefaultInput = definition.DefaultInput;
        record.DefaultForm = definition.DefaultFormJson;
        record.EndDate = definition.EndDate;
    }

    private static FlowiseScheduleDefinition ResolveScheduleDefinition(string flowData)
    {
        var startNode = ResolveStartNode(flowData);
        var startInputs = startNode.Inputs;
        var startInputType = ReadString(startInputs, "startInputType");
        var scheduled = string.Equals(startInputType, "scheduleInput", StringComparison.OrdinalIgnoreCase);
        return new FlowiseScheduleDefinition
        {
            NodeId = startNode.NodeId,
            CronExpression = ReadString(startInputs, "scheduleCronExpression", "cronExpression"),
            DefaultFormJson = ReadRawJson(startInputs, "scheduleFormDefaults", "defaultForm"),
            DefaultInput = ReadString(startInputs, "scheduleDefaultInput", "defaultInput"),
            EndDate = ReadDate(startInputs, "scheduleEndDate", "endDate"),
            IsScheduled = scheduled,
            ScheduleInputMode = ReadString(startInputs, "scheduleInputMode") ?? "text",
            Timezone = ReadString(startInputs, "scheduleTimezone", "timezone") ?? "UTC"
        };
    }

    private static FlowiseStartNodeDefinition ResolveStartNode(string flowData)
    {
        try
        {
            using var document = JsonDocument.Parse(flowData);
            if (!document.RootElement.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            {
                return new FlowiseStartNodeDefinition(null, null);
            }

            foreach (var node in nodes.EnumerateArray())
            {
                if (!node.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = ReadString(data, "name", "nodeType");
                if (!string.Equals(name, "startAgentflow", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var nodeId = ReadString(node, "id");
                if (data.TryGetProperty("inputs", out var inputs) && inputs.ValueKind == JsonValueKind.Object)
                {
                    return new FlowiseStartNodeDefinition(nodeId, inputs.Clone());
                }

                if (data.TryGetProperty("config", out var config) && config.ValueKind == JsonValueKind.Object)
                {
                    return new FlowiseStartNodeDefinition(nodeId, config.Clone());
                }
            }
        }
        catch (JsonException)
        {
            return new FlowiseStartNodeDefinition(null, null);
        }

        return new FlowiseStartNodeDefinition(null, null);
    }

    private static string? ReadString(JsonElement? source, params string[] names)
    {
        if (source is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (source.Value.TryGetProperty(name, out var value))
            {
                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
            }
        }

        return null;
    }

    private static string? ReadRawJson(JsonElement? source, params string[] names)
    {
        if (source is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (source.Value.TryGetProperty(name, out var value))
            {
                return value.GetRawText();
            }
        }

        return null;
    }

    private static DateTime? ReadDate(JsonElement? source, params string[] names)
    {
        var value = ReadString(source, names);
        return DateTime.TryParse(value, out var date) ? date.ToUniversalTime() : null;
    }

    private static string NormalizeType(string? value)
    {
        var type = string.IsNullOrWhiteSpace(value) ? FlowiseChatflowTypes.Chatflow : value.Trim().ToUpperInvariant();
        return type == FlowiseChatflowTypes.Chatflow ||
               type == FlowiseChatflowTypes.Agentflow ||
               type == FlowiseChatflowTypes.Multiagent ||
               type == FlowiseChatflowTypes.Assistant
            ? type
            : throw new ValidationException("Flowise ChatFlow type 不受支持", ErrorCodes.ParameterInvalid);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int NormalizePageIndex(int pageIndex) => Math.Max(pageIndex, 1);

    private static int NormalizePageSize(int pageSize) => Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

    private sealed record FlowiseStartNodeDefinition(string? NodeId, JsonElement? Inputs);

    private sealed class FlowiseScheduleDefinition
    {
        public bool IsScheduled { get; set; }

        public string? NodeId { get; set; }

        public string? CronExpression { get; set; }

        public string? Timezone { get; set; }

        public string ScheduleInputMode { get; set; } = "text";

        public string? DefaultInput { get; set; }

        public string? DefaultFormJson { get; set; }

        public DateTime? EndDate { get; set; }

        public FlowiseScheduleStatusDto ToStatus(bool enabled) => new()
        {
            CronExpression = CronExpression,
            DefaultFormJson = DefaultFormJson,
            DefaultInput = DefaultInput,
            Enabled = enabled,
            EndDate = EndDate,
            IsScheduled = IsScheduled,
            ScheduleInputMode = ScheduleInputMode,
            Timezone = Timezone
        };
    }
}

using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiSettingsService(ISqlSugarClient db, AiWorkspaceContext workspaceContext)
{
    private const int DefaultRetentionDays = 180;
    private const int DefaultCleanupBatchSize = 500;

    public async Task<AiSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Queryable<AiSystemSettingEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);
        return MapSettings(rows);
    }

    public async Task<AiSettingsDto> UpdateAsync(AiSettingsUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var settings = NormalizeUpdate(request);
        await UpsertSettingsAsync(settings, cancellationToken);
        await WriteAuditAsync("settings.updated", "AiSettings", null, "{}", cancellationToken);
        return await GetAsync(cancellationToken);
    }

    public async Task<AiSettingsExportDto> ExportAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetAsync(cancellationToken);
        var prompts = await db.Queryable<AiPromptTemplateEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var agents = await db.Queryable<AiAgentProfileEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var tools = await db.Queryable<AiToolDefinitionEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.ToolCode)
            .ToListAsync(cancellationToken);

        await WriteAuditAsync("settings.exported", "AiSettings", null, "{}", cancellationToken);
        return new AiSettingsExportDto
        {
            Settings = settings,
            PromptTemplates = prompts.Select(MapPrompt).ToList(),
            AgentProfiles = agents.Select(MapAgent).ToList(),
            ToolDefinitions = tools.Select(AiToolManagementService.MapToolDefinition).ToList(),
            ExportedAt = DateTime.UtcNow
        };
    }

    public async Task<AiSettingsImportResultDto> ImportAsync(AiSettingsImportRequest request, CancellationToken cancellationToken = default)
    {
        var result = new AiSettingsImportResultDto();
        if (request.Settings is not null)
        {
            await UpsertSettingsAsync(Normalize(request.Settings), cancellationToken);
            result.SettingsUpdated = 1;
        }

        foreach (var prompt in request.PromptTemplates)
        {
            await UpsertPromptAsync(prompt, cancellationToken);
            result.PromptTemplatesImported++;
        }

        foreach (var agent in request.AgentProfiles)
        {
            await UpsertAgentAsync(agent, cancellationToken);
            result.AgentProfilesImported++;
        }

        foreach (var tool in request.ToolDefinitions)
        {
            await AiToolManagementService.UpsertToolDefinitionAsync(db, workspaceContext.Resolve(), tool, cancellationToken);
            result.ToolDefinitionsImported++;
        }

        await WriteAuditAsync("settings.imported", "AiSettings", null, global::System.Text.Json.JsonSerializer.Serialize(result), cancellationToken);
        return result;
    }

    public async Task<AiCleanupResultDto> CleanupAsync(AiCleanupRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetAsync(cancellationToken);
        var retentionDays = Math.Clamp(request.RetentionDays ?? settings.LogRetentionDays, 1, 3650);
        var batchSize = Math.Clamp(request.BatchSize ?? settings.CleanupBatchSize, 1, 5000);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var usageLogs = await db.Queryable<AiUsageLogEntity>()
            .Where(item => !item.IsDeleted && item.CreatedTime < cutoff)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var row in usageLogs)
        {
            row.IsDeleted = true;
            row.DeletedTime = DateTime.UtcNow;
        }

        var toolLogs = await db.Queryable<AiToolExecutionLogEntity>()
            .Where(item => !item.IsDeleted && item.CreatedTime < cutoff)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var row in toolLogs)
        {
            row.IsDeleted = true;
            row.DeletedTime = DateTime.UtcNow;
        }

        var indexTasks = await db.Queryable<AiKnowledgeIndexTaskEntity>()
            .Where(item => !item.IsDeleted && item.CreatedTime < cutoff && (item.Status == "Succeeded" || item.Status == "Failed"))
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var row in indexTasks)
        {
            row.IsDeleted = true;
            row.DeletedTime = DateTime.UtcNow;
        }

        if (usageLogs.Count > 0)
        {
            await db.Updateable(usageLogs).ExecuteCommandAsync(cancellationToken);
        }

        if (toolLogs.Count > 0)
        {
            await db.Updateable(toolLogs).ExecuteCommandAsync(cancellationToken);
        }

        if (indexTasks.Count > 0)
        {
            await db.Updateable(indexTasks).ExecuteCommandAsync(cancellationToken);
        }

        var result = new AiCleanupResultDto
        {
            UsageLogsDeleted = usageLogs.Count,
            ToolExecutionsDeleted = toolLogs.Count,
            IndexTasksDeleted = indexTasks.Count
        };
        await WriteAuditAsync("settings.cleanup", "AiSettings", null, global::System.Text.Json.JsonSerializer.Serialize(result), cancellationToken);
        return result;
    }

    private async Task UpsertSettingsAsync(AiSettingsDto settings, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string?>
        {
            ["DefaultProviderId"] = settings.DefaultProviderId,
            ["DefaultModelConfigId"] = settings.DefaultModelConfigId,
            ["DefaultAgentProfileId"] = settings.DefaultAgentProfileId,
            ["DefaultPromptTemplateId"] = settings.DefaultPromptTemplateId,
            ["NotificationSettingsJson"] = settings.NotificationSettingsJson,
            ["LogRetentionDays"] = settings.LogRetentionDays.ToString(),
            ["CleanupBatchSize"] = settings.CleanupBatchSize.ToString()
        };

        foreach (var (key, value) in values)
        {
            await UpsertSettingAsync(key, value ?? string.Empty, cancellationToken);
        }
    }

    private async Task UpsertSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var existing = await db.Queryable<AiSystemSettingEntity>()
            .FirstAsync(item => item.SettingKey == key && !item.IsDeleted, cancellationToken);
        if (existing is null)
        {
            await db.Insertable(new AiSystemSettingEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                SettingKey = key,
                SettingValue = value,
                ValueType = key.EndsWith("Days", StringComparison.OrdinalIgnoreCase) || key.EndsWith("Size", StringComparison.OrdinalIgnoreCase) ? "Number" : "String"
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        existing.SettingValue = value;
        existing.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
    }

    private async Task UpsertPromptAsync(AiPromptTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateCode) || string.IsNullOrWhiteSpace(request.TemplateName) || string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            throw new ValidationException("导入提示词缺少编码、名称或系统提示词", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var code = request.TemplateCode.Trim().ToLowerInvariant();
        var existing = await db.Queryable<AiPromptTemplateEntity>().FirstAsync(item => !item.IsDeleted && item.TemplateCode == code, cancellationToken);
        var entity = existing ?? new AiPromptTemplateEntity { TenantId = workspace.TenantId, AppCode = workspace.AppCode };
        entity.TemplateCode = code;
        entity.TemplateName = request.TemplateName.Trim();
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim();
        entity.SystemPrompt = request.SystemPrompt.Trim();
        entity.UserPromptTemplate = NormalizeOptional(request.UserPromptTemplate);
        entity.VariablesJson = NormalizeOptional(request.VariablesJson);
        entity.IsEnabled = request.IsEnabled;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedTime = existing is null ? null : DateTime.UtcNow;

        if (existing is null)
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }
    }

    private async Task UpsertAgentAsync(AiAgentProfileUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AgentCode) || string.IsNullOrWhiteSpace(request.AgentName) || string.IsNullOrWhiteSpace(request.RolePrompt))
        {
            throw new ValidationException("导入智能体缺少编码、名称或角色提示词", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var code = request.AgentCode.Trim().ToLowerInvariant();
        var existing = await db.Queryable<AiAgentProfileEntity>().FirstAsync(item => !item.IsDeleted && item.AgentCode == code, cancellationToken);
        var entity = existing ?? new AiAgentProfileEntity { TenantId = workspace.TenantId, AppCode = workspace.AppCode };
        entity.AgentCode = code;
        entity.AgentName = request.AgentName.Trim();
        entity.RolePrompt = request.RolePrompt.Trim();
        entity.ModelConfigId = NormalizeOptional(request.ModelConfigId);
        entity.PromptTemplateId = NormalizeOptional(request.PromptTemplateId);
        entity.AllowedFunctionsJson = NormalizeOptional(request.AllowedFunctionsJson);
        entity.IsCoordinator = request.IsCoordinator;
        entity.IsEnabled = request.IsEnabled;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedTime = existing is null ? null : DateTime.UtcNow;

        if (existing is null)
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }
    }

    private async Task WriteAuditAsync(string eventType, string resourceType, string? resourceId, string detailJson, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        await db.Insertable(new AiAuditEventEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            EventType = eventType,
            ResourceType = resourceType,
            ResourceId = resourceId,
            UserId = workspace.UserId,
            DetailJson = detailJson
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static AiSettingsDto Normalize(AiSettingsDto request) => new()
    {
        DefaultProviderId = NormalizeOptional(request.DefaultProviderId),
        DefaultModelConfigId = NormalizeOptional(request.DefaultModelConfigId),
        DefaultAgentProfileId = NormalizeOptional(request.DefaultAgentProfileId),
        DefaultPromptTemplateId = NormalizeOptional(request.DefaultPromptTemplateId),
        NotificationSettingsJson = string.IsNullOrWhiteSpace(request.NotificationSettingsJson) ? "{}" : request.NotificationSettingsJson.Trim(),
        LogRetentionDays = Math.Clamp(request.LogRetentionDays <= 0 ? DefaultRetentionDays : request.LogRetentionDays, 1, 3650),
        CleanupBatchSize = Math.Clamp(request.CleanupBatchSize <= 0 ? DefaultCleanupBatchSize : request.CleanupBatchSize, 1, 5000)
    };

    private static AiSettingsDto NormalizeUpdate(AiSettingsUpdateRequest request) => Normalize(new AiSettingsDto
    {
        DefaultProviderId = request.DefaultProviderId,
        DefaultModelConfigId = request.DefaultModelConfigId,
        DefaultAgentProfileId = request.DefaultAgentProfileId,
        DefaultPromptTemplateId = request.DefaultPromptTemplateId,
        NotificationSettingsJson = request.NotificationSettingsJson,
        LogRetentionDays = request.LogRetentionDays,
        CleanupBatchSize = request.CleanupBatchSize
    });

    private static AiSettingsDto MapSettings(IEnumerable<AiSystemSettingEntity> rows)
    {
        var map = rows
            .Where(item => !string.IsNullOrWhiteSpace(item.SettingKey))
            .GroupBy(item => item.SettingKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.UpdatedTime ?? item.CreatedTime)
                    .ThenByDescending(item => item.CreatedTime)
                    .First()
                    .SettingValue,
                StringComparer.OrdinalIgnoreCase);

        return new AiSettingsDto
        {
            DefaultProviderId = ReadOptional(map, "DefaultProviderId"),
            DefaultModelConfigId = ReadOptional(map, "DefaultModelConfigId"),
            DefaultAgentProfileId = ReadOptional(map, "DefaultAgentProfileId"),
            DefaultPromptTemplateId = ReadOptional(map, "DefaultPromptTemplateId"),
            NotificationSettingsJson = ReadOptional(map, "NotificationSettingsJson") ?? "{}",
            LogRetentionDays = ReadInt(map, "LogRetentionDays", DefaultRetentionDays),
            CleanupBatchSize = ReadInt(map, "CleanupBatchSize", DefaultCleanupBatchSize)
        };
    }

    private static AiPromptTemplateDto MapPrompt(AiPromptTemplateEntity entity) => new()
    {
        Id = entity.Id,
        TemplateCode = entity.TemplateCode,
        TemplateName = entity.TemplateName,
        Category = entity.Category,
        SystemPrompt = entity.SystemPrompt,
        UserPromptTemplate = entity.UserPromptTemplate,
        VariablesJson = entity.VariablesJson,
        IsEnabled = entity.IsEnabled,
        SortOrder = entity.SortOrder
    };

    private static AiAgentProfileDto MapAgent(AiAgentProfileEntity entity) => new()
    {
        Id = entity.Id,
        AgentCode = entity.AgentCode,
        AgentName = entity.AgentName,
        RolePrompt = entity.RolePrompt,
        ModelConfigId = entity.ModelConfigId,
        PromptTemplateId = entity.PromptTemplateId,
        AllowedFunctionsJson = entity.AllowedFunctionsJson,
        IsCoordinator = entity.IsCoordinator,
        IsEnabled = entity.IsEnabled,
        SortOrder = entity.SortOrder
    };

    private static string? ReadOptional(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static int ReadInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

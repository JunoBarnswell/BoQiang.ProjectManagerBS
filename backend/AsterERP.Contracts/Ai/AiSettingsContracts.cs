namespace AsterERP.Contracts.Ai;

public sealed class AiSettingsDto
{
    public string? DefaultProviderId { get; set; }

    public string? DefaultModelConfigId { get; set; }

    public string? DefaultAgentProfileId { get; set; }

    public string? DefaultPromptTemplateId { get; set; }

    public string NotificationSettingsJson { get; set; } = "{}";

    public int LogRetentionDays { get; set; } = 180;

    public int CleanupBatchSize { get; set; } = 500;
}

public sealed class AiSettingsUpdateRequest
{
    public string? DefaultProviderId { get; set; }

    public string? DefaultModelConfigId { get; set; }

    public string? DefaultAgentProfileId { get; set; }

    public string? DefaultPromptTemplateId { get; set; }

    public string NotificationSettingsJson { get; set; } = "{}";

    public int LogRetentionDays { get; set; } = 180;

    public int CleanupBatchSize { get; set; } = 500;
}

public sealed class AiSettingsExportDto
{
    public AiSettingsDto Settings { get; set; } = new();

    public IReadOnlyList<AiPromptTemplateDto> PromptTemplates { get; set; } = [];

    public IReadOnlyList<AiAgentProfileDto> AgentProfiles { get; set; } = [];

    public IReadOnlyList<AiToolDefinitionDto> ToolDefinitions { get; set; } = [];

    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AiSettingsImportRequest
{
    public AiSettingsDto? Settings { get; set; }

    public IReadOnlyList<AiPromptTemplateUpsertRequest> PromptTemplates { get; set; } = [];

    public IReadOnlyList<AiAgentProfileUpsertRequest> AgentProfiles { get; set; } = [];

    public IReadOnlyList<AiToolDefinitionUpsertRequest> ToolDefinitions { get; set; } = [];
}

public sealed class AiSettingsImportResultDto
{
    public int SettingsUpdated { get; set; }

    public int PromptTemplatesImported { get; set; }

    public int AgentProfilesImported { get; set; }

    public int ToolDefinitionsImported { get; set; }
}

public sealed class AiCleanupRequest
{
    public int? RetentionDays { get; set; }

    public int? BatchSize { get; set; }
}

public sealed class AiCleanupResultDto
{
    public int ConversationsArchived { get; set; }

    public int UsageLogsDeleted { get; set; }

    public int ToolExecutionsDeleted { get; set; }

    public int IndexTasksDeleted { get; set; }
}

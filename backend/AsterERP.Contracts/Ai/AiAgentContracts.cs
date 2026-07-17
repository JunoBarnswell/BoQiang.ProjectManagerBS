namespace AsterERP.Contracts.Ai;

public sealed class AiAgentProfileDto
{
    public string Id { get; set; } = string.Empty;

    public string AgentCode { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;

    public string RolePrompt { get; set; } = string.Empty;

    public string? ModelConfigId { get; set; }

    public string? PromptTemplateId { get; set; }

    public string? AllowedFunctionsJson { get; set; }

    public bool IsCoordinator { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}

public sealed class AiAgentProfileUpsertRequest
{
    public string AgentCode { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;

    public string RolePrompt { get; set; } = string.Empty;

    public string? ModelConfigId { get; set; }

    public string? PromptTemplateId { get; set; }

    public string? AllowedFunctionsJson { get; set; }

    public bool IsCoordinator { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}

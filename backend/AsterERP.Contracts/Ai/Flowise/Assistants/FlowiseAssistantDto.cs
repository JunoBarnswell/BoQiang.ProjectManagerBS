namespace AsterERP.Contracts.Ai.Flowise.Assistants;

public sealed class FlowiseAssistantDto
{
    public string Id { get; set; } = string.Empty;

    public string AssistantKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? WorkspaceName { get; set; }

    public string AssistantType { get; set; } = "custom";

    public string Status { get; set; } = "Enabled";

    public FlowiseAssistantDefinitionDto Definition { get; set; } = new();

    public string AdvancedMetadataJson { get; set; } = "{}";

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

namespace AsterERP.Contracts.Ai.Flowise.Assistants;

public sealed class FlowiseAssistantUpsertRequest
{
    public string AssistantKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? AssistantType { get; set; }

    public string? Status { get; set; }

    public FlowiseAssistantDefinitionDto Definition { get; set; } = new();

    public string? AdvancedMetadataJson { get; set; }
}

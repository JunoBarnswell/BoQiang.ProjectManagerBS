namespace AsterERP.Contracts.Ai.Flowise.Evaluations;

public sealed class FlowiseEvaluationListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string EvaluationKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? Category { get; set; }

    public string Status { get; set; } = "Draft";

    public FlowiseEvaluationDefinitionDto Definition { get; set; } = new();

    public string AdvancedMetadataJson { get; set; } = "{}";

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

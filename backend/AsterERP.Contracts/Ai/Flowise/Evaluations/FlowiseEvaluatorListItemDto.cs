namespace AsterERP.Contracts.Ai.Flowise.Evaluations;

public sealed class FlowiseEvaluatorListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string EvaluatorKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? EvaluatorType { get; set; }

    public string Status { get; set; } = "Enabled";

    public FlowiseEvaluatorDefinitionDto Definition { get; set; } = new();

    public string AdvancedMetadataJson { get; set; } = "{}";

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

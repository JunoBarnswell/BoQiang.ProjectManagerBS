namespace AsterERP.Contracts.Ai.Flowise.Evaluations;

public sealed class FlowiseEvaluatorSaveRequest
{
    public string EvaluatorKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? EvaluatorType { get; set; }

    public string? Status { get; set; }

    public FlowiseEvaluatorDefinitionDto Definition { get; set; } = new();

    public string? AdvancedMetadataJson { get; set; }
}

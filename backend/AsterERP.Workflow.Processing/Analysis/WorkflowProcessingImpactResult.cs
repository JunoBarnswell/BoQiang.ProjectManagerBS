namespace AsterERP.Workflow.Processing.Analysis;

public sealed class WorkflowProcessingImpactResult
{
    public string RootNodeId { get; init; } = string.Empty;

    public IReadOnlyList<string> NodeIds { get; init; } = [];

    public IReadOnlyList<string> EdgeIds { get; init; } = [];

    public bool Truncated { get; init; }
}

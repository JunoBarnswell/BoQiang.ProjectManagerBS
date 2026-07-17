namespace AsterERP.Workflow.Processing.Graphs;

public sealed class WorkflowProcessingPath
{
    public IReadOnlyList<string> NodeIds { get; init; } = [];

    public IReadOnlyList<string> EdgeIds { get; init; } = [];

    public int Depth => Math.Max(0, NodeIds.Count - 1);
}

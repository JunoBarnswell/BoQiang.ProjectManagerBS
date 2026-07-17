namespace AsterERP.Workflow.Processing.Graphs;

public sealed class WorkflowProcessingDiff
{
    public IReadOnlyList<string> AddedNodeIds { get; init; } = [];

    public IReadOnlyList<string> RemovedNodeIds { get; init; } = [];

    public IReadOnlyList<string> ChangedNodeIds { get; init; } = [];

    public IReadOnlyList<string> AddedEdgeIds { get; init; } = [];

    public IReadOnlyList<string> RemovedEdgeIds { get; init; } = [];

    public IReadOnlyList<string> ChangedEdgeIds { get; init; } = [];

    public bool HasChanges =>
        AddedNodeIds.Count > 0 ||
        RemovedNodeIds.Count > 0 ||
        ChangedNodeIds.Count > 0 ||
        AddedEdgeIds.Count > 0 ||
        RemovedEdgeIds.Count > 0 ||
        ChangedEdgeIds.Count > 0;
}

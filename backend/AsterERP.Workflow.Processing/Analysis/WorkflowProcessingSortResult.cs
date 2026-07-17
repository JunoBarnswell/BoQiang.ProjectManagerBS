namespace AsterERP.Workflow.Processing.Analysis;

public sealed class WorkflowProcessingSortResult
{
    public bool Succeeded { get; init; }

    public IReadOnlyList<string> NodeIds { get; init; } = [];

    public IReadOnlyList<string> CycleNodeIds { get; init; } = [];
}

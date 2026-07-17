namespace AsterERP.Workflow.Processing.Planning;

public sealed class WorkflowProcessingExecutionPlan
{
    public bool Succeeded { get; init; }

    public IReadOnlyList<WorkflowProcessingExecutionBatch> Batches { get; init; } = [];

    public IReadOnlyList<string> CycleNodeIds { get; init; } = [];
}

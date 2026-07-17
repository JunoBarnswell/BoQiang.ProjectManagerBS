namespace AsterERP.Workflow.Processing.Planning;

public sealed class WorkflowProcessingExecutionBatch
{
    public int BatchIndex { get; init; }

    public IReadOnlyList<string> NodeIds { get; init; } = [];
}

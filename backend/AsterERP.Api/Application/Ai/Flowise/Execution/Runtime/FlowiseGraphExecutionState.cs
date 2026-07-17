namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class FlowiseGraphExecutionState
{
    private readonly Dictionary<string, FlowiseGraphNodeExecution> executions = new(StringComparer.OrdinalIgnoreCase);

    internal IReadOnlyDictionary<string, FlowiseGraphNodeExecution> Executions => executions;

    internal bool IsTerminated { get; private set; }

    internal void Record(FlowiseGraphNodeExecution execution)
    {
        executions[execution.NodeId] = execution;
        if (string.Equals(execution.Status, "FAILED", StringComparison.OrdinalIgnoreCase))
        {
            IsTerminated = true;
        }
    }

    internal void Terminate(string nodeId, FlowiseRuntimeNodeKind kind, string code, string message) =>
        Record(new FlowiseGraphNodeExecution(nodeId, kind, "FAILED", ErrorCode: code, ErrorMessage: message));
}

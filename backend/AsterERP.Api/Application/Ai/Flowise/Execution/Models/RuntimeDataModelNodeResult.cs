using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class RuntimeDataModelNodeResult
{
    public int ExecutionIndex { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string NodeLabel { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public RuntimeQueryRequest Request { get; set; } = new(1, 20, null, null, null);

    public RuntimeQueryResponse Response { get; set; } = new([], [], 0, 1, 20);

    public FlowiseIterationContext? Iteration { get; set; }
}

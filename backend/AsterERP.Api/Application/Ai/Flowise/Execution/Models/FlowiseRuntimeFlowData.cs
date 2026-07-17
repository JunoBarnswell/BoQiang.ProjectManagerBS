using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class FlowiseRuntimeFlowData
{
    public IReadOnlyList<FlowiseRuntimeNode> Nodes { get; set; } = [];

    public IReadOnlyList<FlowiseRuntimeEdge> Edges { get; set; } = [];
}

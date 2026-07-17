using AsterERP.Workflow.Processing.Definitions;

namespace AsterERP.Workflow.Processing.Graphs;

public sealed class WorkflowProcessingGraph
{
    private readonly Dictionary<string, WorkflowProcessingNode> nodesById;
    private readonly Dictionary<string, List<WorkflowProcessingEdge>> outgoingByNodeId;
    private readonly Dictionary<string, List<WorkflowProcessingEdge>> incomingByNodeId;

    public WorkflowProcessingGraph(WorkflowProcessingDefinition definition)
    {
        Definition = definition;
        nodesById = definition.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        outgoingByNodeId = definition.Nodes.ToDictionary(node => node.Id, _ => new List<WorkflowProcessingEdge>(), StringComparer.OrdinalIgnoreCase);
        incomingByNodeId = definition.Nodes.ToDictionary(node => node.Id, _ => new List<WorkflowProcessingEdge>(), StringComparer.OrdinalIgnoreCase);

        foreach (var edge in definition.Edges)
        {
            if (!outgoingByNodeId.TryGetValue(edge.FromNodeId, out var outgoing))
            {
                outgoing = [];
                outgoingByNodeId[edge.FromNodeId] = outgoing;
            }

            if (!incomingByNodeId.TryGetValue(edge.ToNodeId, out var incoming))
            {
                incoming = [];
                incomingByNodeId[edge.ToNodeId] = incoming;
            }

            outgoing.Add(edge);
            incoming.Add(edge);
        }
    }

    public WorkflowProcessingDefinition Definition { get; }

    public IReadOnlyDictionary<string, WorkflowProcessingNode> NodesById => nodesById;

    public IReadOnlyList<WorkflowProcessingEdge> GetOutgoingEdges(string nodeId) =>
        outgoingByNodeId.TryGetValue(nodeId, out var edges) ? edges : [];

    public IReadOnlyList<WorkflowProcessingEdge> GetIncomingEdges(string nodeId) =>
        incomingByNodeId.TryGetValue(nodeId, out var edges) ? edges : [];

    public bool HasNode(string nodeId) => nodesById.ContainsKey(nodeId);
}

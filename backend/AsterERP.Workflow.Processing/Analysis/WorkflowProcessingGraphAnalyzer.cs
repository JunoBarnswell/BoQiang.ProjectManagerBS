using System.Text.Json;
using AsterERP.Workflow.Processing.Definitions;
using AsterERP.Workflow.Processing.Graphs;

namespace AsterERP.Workflow.Processing.Analysis;

public sealed class WorkflowProcessingGraphAnalyzer : IWorkflowProcessingGraphAnalyzer
{
    public WorkflowProcessingSortResult TopologicalSort(WorkflowProcessingDefinition definition)
    {
        var nodeIds = definition.Nodes.Select(node => node.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var indegree = nodeIds.ToDictionary(id => id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var outgoing = nodeIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var edge in definition.Edges)
        {
            if (!indegree.ContainsKey(edge.FromNodeId) || !indegree.ContainsKey(edge.ToNodeId))
            {
                continue;
            }

            outgoing[edge.FromNodeId].Add(edge.ToNodeId);
            indegree[edge.ToNodeId]++;
        }

        var queue = new Queue<string>(nodeIds.Where(id => indegree[id] == 0).Order(StringComparer.OrdinalIgnoreCase));
        var result = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var next in outgoing[current].Order(StringComparer.OrdinalIgnoreCase))
            {
                indegree[next]--;
                if (indegree[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        if (result.Count == nodeIds.Count)
        {
            return new WorkflowProcessingSortResult { Succeeded = true, NodeIds = result };
        }

        return new WorkflowProcessingSortResult
        {
            Succeeded = false,
            NodeIds = result,
            CycleNodeIds = nodeIds.Except(result, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    public IReadOnlyList<WorkflowProcessingPath> FindPaths(
        WorkflowProcessingDefinition definition,
        string fromNodeId,
        string toNodeId,
        int maxDepth,
        int limit)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId))
        {
            return [];
        }

        var graph = new WorkflowProcessingGraph(definition);
        if (!graph.HasNode(fromNodeId) || !graph.HasNode(toNodeId))
        {
            return [];
        }

        var paths = new List<WorkflowProcessingPath>();
        var stack = new Stack<(string NodeId, List<string> NodeIds, List<string> EdgeIds, HashSet<string> Visited)>();
        stack.Push((fromNodeId, [fromNodeId], [], new HashSet<string>(StringComparer.OrdinalIgnoreCase) { fromNodeId }));

        while (stack.Count > 0 && paths.Count < Math.Clamp(limit, 1, 100))
        {
            var current = stack.Pop();
            if (current.NodeIds.Count - 1 > Math.Clamp(maxDepth, 1, 20))
            {
                continue;
            }

            if (string.Equals(current.NodeId, toNodeId, StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(new WorkflowProcessingPath { NodeIds = current.NodeIds, EdgeIds = current.EdgeIds });
                continue;
            }

            foreach (var edge in graph.GetOutgoingEdges(current.NodeId).OrderByDescending(edge => edge.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (current.Visited.Contains(edge.ToNodeId))
                {
                    continue;
                }

                stack.Push((
                    edge.ToNodeId,
                    [.. current.NodeIds, edge.ToNodeId],
                    [.. current.EdgeIds, edge.Id],
                    new HashSet<string>(current.Visited, StringComparer.OrdinalIgnoreCase) { edge.ToNodeId }));
            }
        }

        return paths;
    }

    public WorkflowProcessingImpactResult AnalyzeImpact(
        WorkflowProcessingDefinition definition,
        string rootNodeId,
        int maxDepth,
        int limit)
    {
        var graph = new WorkflowProcessingGraph(definition);
        if (!graph.HasNode(rootNodeId))
        {
            return new WorkflowProcessingImpactResult { RootNodeId = rootNodeId };
        }

        var maxResults = Math.Clamp(limit, 1, 1000);
        var visitedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootNodeId };
        var edgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string NodeId, int Depth)>();
        queue.Enqueue((rootNodeId, 0));
        var truncated = false;

        while (queue.Count > 0)
        {
            var (nodeId, depth) = queue.Dequeue();
            if (depth >= Math.Clamp(maxDepth, 1, 20))
            {
                continue;
            }

            foreach (var edge in graph.GetOutgoingEdges(nodeId))
            {
                edgeIds.Add(edge.Id);
                if (visitedNodes.Count >= maxResults)
                {
                    truncated = true;
                    continue;
                }

                if (visitedNodes.Add(edge.ToNodeId))
                {
                    queue.Enqueue((edge.ToNodeId, depth + 1));
                }
            }
        }

        return new WorkflowProcessingImpactResult
        {
            RootNodeId = rootNodeId,
            NodeIds = visitedNodes.Where(id => !string.Equals(id, rootNodeId, StringComparison.OrdinalIgnoreCase)).ToList(),
            EdgeIds = edgeIds.ToList(),
            Truncated = truncated
        };
    }

    public WorkflowProcessingDiff Diff(WorkflowProcessingDefinition baseline, WorkflowProcessingDefinition candidate)
    {
        var baselineNodes = baseline.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var candidateNodes = candidate.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var baselineEdges = baseline.Edges.ToDictionary(edge => edge.Id, StringComparer.OrdinalIgnoreCase);
        var candidateEdges = candidate.Edges.ToDictionary(edge => edge.Id, StringComparer.OrdinalIgnoreCase);

        return new WorkflowProcessingDiff
        {
            AddedNodeIds = candidateNodes.Keys.Except(baselineNodes.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            RemovedNodeIds = baselineNodes.Keys.Except(candidateNodes.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            ChangedNodeIds = candidateNodes.Keys.Intersect(baselineNodes.Keys, StringComparer.OrdinalIgnoreCase)
                .Where(id => Serialize(candidateNodes[id]) != Serialize(baselineNodes[id]))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            AddedEdgeIds = candidateEdges.Keys.Except(baselineEdges.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            RemovedEdgeIds = baselineEdges.Keys.Except(candidateEdges.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            ChangedEdgeIds = candidateEdges.Keys.Intersect(baselineEdges.Keys, StringComparer.OrdinalIgnoreCase)
                .Where(id => Serialize(candidateEdges[id]) != Serialize(baselineEdges[id]))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value);
}

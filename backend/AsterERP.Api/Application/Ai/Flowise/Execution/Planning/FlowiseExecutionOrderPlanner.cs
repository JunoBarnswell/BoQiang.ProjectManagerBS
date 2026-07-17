namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseExecutionOrderPlanner(
    FlowiseRuntimeNodeClassifier nodeClassifier,
    FlowiseConditionEvaluator conditionEvaluator)
{
    internal FlowiseGraphCursor CreateCursor(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyDictionary<string, BranchDecision>? branchDecisions = null) =>
        new(Plan(flowData, branchDecisions));

    internal IReadOnlyList<FlowiseRuntimeNode> Plan(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyDictionary<string, BranchDecision>? branchDecisions = null)
    {
        var nodesById = flowData.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var rootNodeIds = nodesById.Values
            .Where(node => string.IsNullOrWhiteSpace(node.ParentId))
            .Select(node => node.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reachableIds = ResolveReachableNodeIds(flowData, nodesById, branchDecisions);
        var incomingCounts = nodesById.Keys.ToDictionary(id => id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var outgoing = nodesById.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var edge in flowData.Edges)
        {
            if (!nodesById.ContainsKey(edge.Source) ||
                !nodesById.ContainsKey(edge.Target) ||
                !rootNodeIds.Contains(edge.Source) ||
                !rootNodeIds.Contains(edge.Target) ||
                !reachableIds.Contains(edge.Source) ||
                !reachableIds.Contains(edge.Target))
            {
                continue;
            }

            var sourceNode = nodesById[edge.Source];
            if (!OutgoingEdgesForExecution(flowData, sourceNode, branchDecisions).Any(item => string.Equals(item.Id, edge.Id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            incomingCounts[edge.Target] += 1;
            outgoing[edge.Source].Add(edge.Target);
        }

        var ready = flowData.Nodes
            .Where(node => rootNodeIds.Contains(node.Id) && reachableIds.Contains(node.Id) && incomingCounts[node.Id] == 0)
            .Select(node => node.Id)
            .ToList();
        var ordered = new List<FlowiseRuntimeNode>();
        while (ready.Count > 0)
        {
            var currentId = ready[0];
            ready.RemoveAt(0);
            ordered.Add(nodesById[currentId]);
            foreach (var targetId in outgoing[currentId])
            {
                incomingCounts[targetId] -= 1;
                if (incomingCounts[targetId] == 0)
                {
                    ready.Add(targetId);
                }
            }
        }

        if (ordered.Count == reachableIds.Count)
        {
            return ordered;
        }

        var orderedIds = ordered.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ordered.AddRange(flowData.Nodes.Where(node => rootNodeIds.Contains(node.Id) && reachableIds.Contains(node.Id) && !orderedIds.Contains(node.Id)));
        return ordered;
    }

    private IReadOnlySet<string> ResolveReachableNodeIds(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyDictionary<string, FlowiseRuntimeNode> nodesById,
        IReadOnlyDictionary<string, BranchDecision>? branchDecisions)
    {
        var rootNodes = flowData.Nodes.Where(node => string.IsNullOrWhiteSpace(node.ParentId)).ToList();
        var rootNodeIds = rootNodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allTargets = flowData.Edges
            .Where(edge => rootNodeIds.Contains(edge.Source) && rootNodeIds.Contains(edge.Target))
            .Select(edge => edge.Target)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ready = rootNodes.Where(node => !allTargets.Contains(node.Id)).Select(node => node.Id).ToList();
        if (ready.Count == 0)
        {
            ready.AddRange(rootNodes.Take(1).Select(node => node.Id));
        }

        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (ready.Count > 0)
        {
            var currentId = ready[0];
            ready.RemoveAt(0);
            if (!reachable.Add(currentId) || !nodesById.TryGetValue(currentId, out var node))
            {
                continue;
            }

            foreach (var edge in OutgoingEdgesForExecution(flowData, node, branchDecisions))
            {
                if (rootNodeIds.Contains(edge.Target) && nodesById.ContainsKey(edge.Target) && !reachable.Contains(edge.Target))
                {
                    ready.Add(edge.Target);
                }
            }
        }

        return reachable;
    }

    internal IReadOnlyList<FlowiseRuntimeEdge> OutgoingEdgesForExecution(
        FlowiseRuntimeFlowData flowData,
        FlowiseRuntimeNode node,
        IReadOnlyDictionary<string, BranchDecision>? branchDecisions)
    {
        var edges = flowData.Edges
            .Where(edge => string.Equals(edge.Source, node.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(edge => conditionEvaluator.ResolveEdgeOutputIndex(edge, node.Id))
            .ToList();
        if (!nodeClassifier.IsConditionNode(node) || branchDecisions is null || !branchDecisions.TryGetValue(node.Id, out var decision))
        {
            return edges;
        }

        return edges
            .Where(edge =>
                string.Equals(edge.Target, decision.TargetNodeId, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(decision.SourceHandle) && string.Equals(edge.SourceHandle, decision.SourceHandle, StringComparison.OrdinalIgnoreCase)) ||
                conditionEvaluator.ResolveEdgeOutputIndex(edge, node.Id) == decision.SelectedIndex)
            .Take(1)
            .ToList();
    }
}

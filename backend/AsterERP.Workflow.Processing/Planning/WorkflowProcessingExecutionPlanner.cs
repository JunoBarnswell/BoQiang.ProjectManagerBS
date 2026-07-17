using AsterERP.Workflow.Processing.Analysis;
using AsterERP.Workflow.Processing.Definitions;

namespace AsterERP.Workflow.Processing.Planning;

public sealed class WorkflowProcessingExecutionPlanner(IWorkflowProcessingGraphAnalyzer analyzer) : IWorkflowProcessingExecutionPlanner
{
    public WorkflowProcessingExecutionPlan Plan(WorkflowProcessingDefinition definition)
    {
        var sort = analyzer.TopologicalSort(definition);
        if (!sort.Succeeded)
        {
            return new WorkflowProcessingExecutionPlan
            {
                Succeeded = false,
                CycleNodeIds = sort.CycleNodeIds
            };
        }

        var nodeIds = definition.Nodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependencies = nodeIds.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (var edge in definition.Edges)
        {
            if (dependencies.ContainsKey(edge.ToNodeId) && nodeIds.Contains(edge.FromNodeId))
            {
                dependencies[edge.ToNodeId].Add(edge.FromNodeId);
            }
        }

        foreach (var node in definition.Nodes)
        {
            if (!dependencies.TryGetValue(node.Id, out var set))
            {
                continue;
            }

            foreach (var dependency in node.DependsOn.Where(nodeIds.Contains))
            {
                set.Add(dependency);
            }
        }

        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var remaining = new HashSet<string>(nodeIds, StringComparer.OrdinalIgnoreCase);
        var batches = new List<WorkflowProcessingExecutionBatch>();

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(nodeId => dependencies[nodeId].All(completed.Contains))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ready.Count == 0)
            {
                return new WorkflowProcessingExecutionPlan
                {
                    Succeeded = false,
                    Batches = batches,
                    CycleNodeIds = remaining.Order(StringComparer.OrdinalIgnoreCase).ToList()
                };
            }

            batches.Add(new WorkflowProcessingExecutionBatch { BatchIndex = batches.Count, NodeIds = ready });
            foreach (var nodeId in ready)
            {
                remaining.Remove(nodeId);
                completed.Add(nodeId);
            }
        }

        return new WorkflowProcessingExecutionPlan { Succeeded = true, Batches = batches };
    }
}

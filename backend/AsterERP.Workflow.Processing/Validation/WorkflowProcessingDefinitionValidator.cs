using AsterERP.Workflow.Processing.Analysis;
using AsterERP.Workflow.Processing.Definitions;

namespace AsterERP.Workflow.Processing.Validation;

public sealed class WorkflowProcessingDefinitionValidator(IWorkflowProcessingGraphAnalyzer analyzer) : IWorkflowProcessingDefinitionValidator
{
    public WorkflowProcessingValidationResult Validate(WorkflowProcessingDefinition definition)
    {
        var issues = new List<WorkflowProcessingValidationIssue>();
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            AddIssue(issues, "DefinitionIdRequired", "定义 Id 不能为空", field: nameof(definition.Id));
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            AddIssue(issues, "DefinitionNameRequired", "定义名称不能为空", field: nameof(definition.Name));
        }

        if (definition.Nodes.Count == 0)
        {
            AddIssue(issues, "GraphEmpty", "工作流图至少需要一个节点", field: nameof(definition.Nodes));
            return new WorkflowProcessingValidationResult { Issues = issues };
        }

        ValidateNodes(definition, issues);
        ValidateEdges(definition, issues);
        ValidateEntrypoints(definition, issues);

        if (definition.RequiresAcyclicGraph)
        {
            var sort = analyzer.TopologicalSort(definition);
            if (!sort.Succeeded)
            {
                AddIssue(issues, "GraphCycleDetected", "工作流执行图存在环路，不能生成 DAG 执行计划");
            }
        }

        return new WorkflowProcessingValidationResult { Issues = issues };
    }

    private static void ValidateNodes(WorkflowProcessingDefinition definition, List<WorkflowProcessingValidationIssue> issues)
    {
        var duplicateNodeIds = definition.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var nodeId in duplicateNodeIds)
        {
            AddIssue(issues, "NodeIdDuplicate", $"节点 Id 重复：{nodeId}", nodeId);
        }

        foreach (var node in definition.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                AddIssue(issues, "NodeIdRequired", "节点 Id 不能为空", field: nameof(node.Id));
            }

            if (string.IsNullOrWhiteSpace(node.Name))
            {
                AddIssue(issues, "NodeNameRequired", "节点名称不能为空", node.Id, field: nameof(node.Name));
            }
        }
    }

    private static void ValidateEdges(WorkflowProcessingDefinition definition, List<WorkflowProcessingValidationIssue> issues)
    {
        var nodeIds = definition.Nodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicateEdgeIds = definition.Edges
            .Where(edge => !string.IsNullOrWhiteSpace(edge.Id))
            .GroupBy(edge => edge.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var edgeId in duplicateEdgeIds)
        {
            AddIssue(issues, "EdgeIdDuplicate", $"边 Id 重复：{edgeId}", edgeId: edgeId);
        }

        foreach (var edge in definition.Edges)
        {
            if (string.IsNullOrWhiteSpace(edge.Id))
            {
                AddIssue(issues, "EdgeIdRequired", "边 Id 不能为空", edgeId: edge.Id, field: nameof(edge.Id));
            }

            if (!nodeIds.Contains(edge.FromNodeId))
            {
                AddIssue(issues, "EdgeFromNodeMissing", $"边 {edge.Id} 的起点节点不存在", edge.FromNodeId, edge.Id, nameof(edge.FromNodeId));
            }

            if (!nodeIds.Contains(edge.ToNodeId))
            {
                AddIssue(issues, "EdgeToNodeMissing", $"边 {edge.Id} 的终点节点不存在", edge.ToNodeId, edge.Id, nameof(edge.ToNodeId));
            }
        }
    }

    private static void ValidateEntrypoints(WorkflowProcessingDefinition definition, List<WorkflowProcessingValidationIssue> issues)
    {
        var incoming = definition.Edges.GroupBy(edge => edge.ToNodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var outgoing = definition.Edges.GroupBy(edge => edge.FromNodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        if (!definition.Nodes.Any(node => node.IsEntry || !incoming.ContainsKey(node.Id)))
        {
            AddIssue(issues, "EntryNodeMissing", "工作流图缺少入口节点");
        }

        if (!definition.Nodes.Any(node => node.IsExit || !outgoing.ContainsKey(node.Id)))
        {
            AddIssue(issues, "ExitNodeMissing", "工作流图缺少出口节点");
        }
    }

    private static void AddIssue(
        List<WorkflowProcessingValidationIssue> issues,
        string code,
        string message,
        string? nodeId = null,
        string? edgeId = null,
        string? field = null)
    {
        issues.Add(new WorkflowProcessingValidationIssue
        {
            ErrorCode = code,
            Message = message,
            NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId,
            EdgeId = string.IsNullOrWhiteSpace(edgeId) ? null : edgeId,
            Field = field
        });
    }
}

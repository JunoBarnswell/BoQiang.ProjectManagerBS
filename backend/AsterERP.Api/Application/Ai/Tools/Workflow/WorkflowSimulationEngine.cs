using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowSimulationEngine(WorkflowConditionEvaluator conditionEvaluator)
{
    public IReadOnlyList<AiWorkflowSimulationStepDto> Simulate(
        AiWorkflowDraftDto draft,
        IReadOnlyDictionary<string, object?> variables)
    {
        var start = draft.Nodes.FirstOrDefault(item => item.Type.Equals("startEvent", StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException("流程缺少开始节点，无法模拟", ErrorCodes.AiWorkflowSimulationFailed);
        var steps = new List<AiWorkflowSimulationStepDto>();
        var current = start;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index <= 100; index++)
        {
            if (!visited.Add($"{current.Id}:{index}") || current.Type.Equals("endEvent", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add(BuildStep(index, current, null, true));
                break;
            }

            var outgoing = draft.Edges.Where(item => string.Equals(item.SourceId, current.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            var matched = outgoing.FirstOrDefault(item => conditionEvaluator.Evaluate(item.Condition, variables));
            steps.Add(BuildStep(index, current, matched, matched is null || conditionEvaluator.Evaluate(matched.Condition, variables)));
            if (matched is null)
            {
                break;
            }

            current = draft.Nodes.FirstOrDefault(item => string.Equals(item.Id, matched.TargetId, StringComparison.OrdinalIgnoreCase))
                      ?? throw new ValidationException($"模拟路径引用了不存在的节点：{matched.TargetId}", ErrorCodes.AiWorkflowSimulationFailed);
        }

        return steps;
    }

    private static AiWorkflowSimulationStepDto BuildStep(
        int sortOrder,
        AiWorkflowDraftNodeDto node,
        AiWorkflowDraftEdgeDto? edge,
        bool matched) => new()
    {
        SortOrder = sortOrder,
        NodeId = node.Id,
        NodeName = node.Name,
        Action = node.Type.Equals("endEvent", StringComparison.OrdinalIgnoreCase) ? "complete" : "enter",
        MatchedEdgeId = edge?.Id,
        Condition = edge?.Condition,
        ConditionMatched = matched,
        Summary = edge is null
            ? $"进入 {node.Name}"
            : $"进入 {node.Name}，下一步 {edge.TargetId}"
    };
}

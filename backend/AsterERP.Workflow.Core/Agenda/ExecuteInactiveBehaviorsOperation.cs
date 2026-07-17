using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Agenda;

public class ExecuteInactiveBehaviorsOperation : AbstractOperation
{
    private readonly List<ExecutionEntity> _involvedExecutions;

    public ExecuteInactiveBehaviorsOperation(
        IAgenda agenda,
        IEnumerable<ExecutionEntity> involvedExecutions,
        IProcessEngineConfiguration engineConfig)
        : base(agenda, null, engineConfig)
    {
        _involvedExecutions = involvedExecutions?.ToList() ?? new List<ExecutionEntity>();
    }

    public override async Task RunAsync(CancellationToken cancellationToken = default)
    {
        foreach (var executionEntity in _involvedExecutions)
        {
            var process = executionEntity.Process;
            if (process == null) continue;

            var flowNodeIdsWithInactivatedBehavior = new List<string>();
            foreach (var flowElement in process.FlowElements)
            {
                if (flowElement is BpmnModelNs.FlowNode flowNode &&
                    flowNode.Behavior is Behavior.IInactiveActivityBehavior)
                {
                    flowNodeIdsWithInactivatedBehavior.Add(flowNode.Id);
                }
            }

            if (flowNodeIdsWithInactivatedBehavior.Count > 0)
            {
                var inactiveExecutions = FindInactiveExecutions(executionEntity);
                foreach (var inactiveExecution in inactiveExecutions)
                {
                    if (!inactiveExecution.IsActive &&
                        flowNodeIdsWithInactivatedBehavior.Contains(inactiveExecution.CurrentActivityId) &&
                        !inactiveExecution.IsEnded)
                    {
                        var flowNode = process.FlowElements.Find(e => e.Id == inactiveExecution.CurrentActivityId) as BpmnModelNs.FlowNode;
                        if (flowNode?.Behavior is Behavior.IInactiveActivityBehavior inactiveActivityBehavior)
                        {
                            if (flowNode.Behavior is Behavior.FlowNodeActivityBehavior flowNodeBehavior)
                            {
                                flowNodeBehavior.Agenda = Agenda;
                            }
                            await inactiveActivityBehavior.ExecuteInactiveAsync(inactiveExecution, cancellationToken);
                        }
                    }
                }
            }
        }
    }

    private List<ExecutionEntity> FindInactiveExecutions(ExecutionEntity executionEntity)
    {
        var result = new List<ExecutionEntity>();
        CollectInactiveExecutions(executionEntity, result);
        return result;
    }

    private void CollectInactiveExecutions(ExecutionEntity execution, List<ExecutionEntity> result)
    {
        if (!execution.IsActive && !execution.IsEnded)
        {
            result.Add(execution);
        }

        foreach (var child in execution.ChildExecutions)
        {
            CollectInactiveExecutions(child, result);
        }
    }
}

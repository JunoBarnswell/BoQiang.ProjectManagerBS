using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class EventGatewayActivityBehavior : FlowNodeActivityBehavior
{
    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (execution.CurrentFlowElement is not BpmnModelNs.EventGateway gateway)
        {
            throw new WorkflowEngineException("Event gateway execution requires CurrentFlowElement to be EventGateway.");
        }

        if (gateway.OutgoingFlows == null || gateway.OutgoingFlows.Count == 0)
        {
            throw new WorkflowEngineException($"EventGateway '{gateway.Id}' has no outgoing sequence flows.");
        }

        execution.IsActive = false;
        execution.SetVariableLocal("_eventGatewayWaiting", true);
        execution.SetVariableLocal("_eventGatewayId", gateway.Id);

        foreach (var sequenceFlow in gateway.OutgoingFlows)
        {
            if (sequenceFlow.TargetFlowElement is not BpmnModelNs.FlowNode targetFlowNode)
            {
                throw new WorkflowEngineException(
                    $"EventGateway '{gateway.Id}' outgoing flow '{sequenceFlow.Id}' has no target flow node.");
            }

            var childExecution = CreateChildExecution(execution, targetFlowNode);
            execution.ChildExecutions.Add(childExecution);
        }

        await Task.CompletedTask;
    }

    private static ExecutionEntity CreateChildExecution(ExecutionEntity parentExecution, BpmnModelNs.FlowNode targetFlowNode)
    {
        return new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid("N"),
            ProcessInstanceId = parentExecution.ProcessInstanceId,
            ProcessDefinitionId = parentExecution.ProcessDefinitionId,
            Parent = parentExecution,
            ParentId = parentExecution.Id,
            IsActive = true,
            IsEnded = false,
            IsScope = false,
            IsConcurrent = true,
            IsProcessInstanceType = false,
            CurrentFlowElement = targetFlowNode,
            CurrentFlowElementId = targetFlowNode.Id,
            ActivityId = targetFlowNode.Id,
            Process = parentExecution.Process,
            Variables = new Dictionary<string, object?>(parentExecution.Variables),
            TenantId = parentExecution.TenantId,
            BusinessKey = parentExecution.BusinessKey
        };
    }
}


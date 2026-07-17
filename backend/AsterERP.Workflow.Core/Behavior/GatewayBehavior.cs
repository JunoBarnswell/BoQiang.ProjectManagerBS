using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Agenda;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Util;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class GatewayActivityBehavior : FlowNodeActivityBehavior
{
    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        await LeaveAsync(execution, cancellationToken);
    }

    protected List<BpmnModelNs.SequenceFlow> EvaluateOutgoingFlows(ExecutionEntity execution, IExpressionManager? expressionManager)
    {
        if (execution.CurrentFlowElement is not BpmnModelNs.FlowNode flowNode) return new();

        var outgoingFlows = flowNode.OutgoingFlows;
        if (outgoingFlows == null || outgoingFlows.Count == 0) return new();

        var defaultFlowId = ResolveDefaultFlowId(execution.CurrentFlowElement);
        var defaultFlow = outgoingFlows.FirstOrDefault(f => f.Id == defaultFlowId);

        var selectedFlows = new List<BpmnModelNs.SequenceFlow>();
        BpmnModelNs.SequenceFlow? defaultSequenceFlow = null;

        foreach (var flow in outgoingFlows)
        {
            if (flow == defaultFlow)
            {
                defaultSequenceFlow = flow;
                continue;
            }

            if (string.IsNullOrEmpty(flow.ConditionExpression))
            {
                selectedFlows.Add(flow);
            }
            else if (expressionManager != null)
            {
                var result = expressionManager.Evaluate(flow.ConditionExpression, execution.Variables);
                if (result is bool boolValue && boolValue)
                {
                    selectedFlows.Add(flow);
                }
            }
        }

        if (selectedFlows.Count == 0 && defaultSequenceFlow != null)
        {
            selectedFlows.Add(defaultSequenceFlow);
        }

        return selectedFlows;
    }

    private static string? ResolveDefaultFlowId(BpmnModelNs.FlowElement flowElement)
    {
        if (flowElement is BpmnModelNs.Activity activity) return activity.DefaultFlow;
        if (flowElement is BpmnModelNs.ExclusiveGateway eg) return eg.DefaultFlow;
        if (flowElement is BpmnModelNs.InclusiveGateway ig) return ig.DefaultFlow;
        if (flowElement is BpmnModelNs.ComplexGateway cg) return cg.DefaultFlow;
        return null;
    }
}

public class ExclusiveGatewayActivityBehavior : GatewayActivityBehavior
{
    private readonly IExpressionManager? _expressionManager;
    private readonly IEventDispatcher? _eventDispatcher;

    public ExclusiveGatewayActivityBehavior(IExpressionManager? expressionManager = null, IEventDispatcher? eventDispatcher = null)
    {
        _expressionManager = expressionManager;
        _eventDispatcher = eventDispatcher;
    }

    public override Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        return LeaveAsync(execution, _expressionManager, cancellationToken);
    }

    protected override Task LeaveAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        return LeaveAsync(execution, _expressionManager, cancellationToken);
    }

    protected override Task LeaveAsync(ExecutionEntity execution, IExpressionManager? expressionManager, CancellationToken cancellationToken = default)
    {
        if (execution.CurrentFlowElement is not BpmnModelNs.ExclusiveGateway gateway)
            return Task.CompletedTask;

        if (_eventDispatcher != null && _eventDispatcher.IsEnabled)
        {
            var completedEvent = WorkflowEventBuilder.CreateActivityCompletedEvent(
                gateway.Id ?? "",
                "exclusiveGateway",
                execution.Id,
                execution.ProcessInstanceId ?? "");
            _eventDispatcher.DispatchEvent(completedEvent);
        }

        BpmnModelNs.SequenceFlow? outgoingFlow = null;
        BpmnModelNs.SequenceFlow? defaultFlow = null;
        var defaultFlowId = gateway.DefaultFlow;

        foreach (var sequenceFlow in gateway.OutgoingFlows)
        {
            if (defaultFlowId != null && defaultFlowId == sequenceFlow.Id)
            {
                defaultFlow = sequenceFlow;
                continue;
            }

            if (expressionManager != null && ConditionUtil.HasTrueCondition(sequenceFlow, execution, expressionManager))
            {
                outgoingFlow = sequenceFlow;
                break;
            }

            if (string.IsNullOrEmpty(sequenceFlow.ConditionExpression))
            {
                outgoingFlow = sequenceFlow;
                break;
            }
        }

        BpmnModelNs.SequenceFlow? selectedSequenceFlow = null;
        if (outgoingFlow != null)
        {
            selectedSequenceFlow = outgoingFlow;
        }
        else if (defaultFlow != null)
        {
            selectedSequenceFlow = defaultFlow;
        }
        else
        {
            throw new AsterERP.Workflow.Common.WorkflowEngineException(
                $"No outgoing sequence flow of the exclusive gateway '{gateway.Id}' could be selected for continuing the process");
        }

        execution.CurrentFlowElement = selectedSequenceFlow;
        execution.CurrentFlowElementId = selectedSequenceFlow.Id;

        return Task.CompletedTask;
    }
}

public class ParallelGatewayActivityBehavior : GatewayActivityBehavior
{
    public override Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (execution.CurrentFlowElement is not BpmnModelNs.ParallelGateway gateway)
            return Task.CompletedTask;

        execution.IsActive = false;

        var joinedExecutions = FindInactiveExecutionsByActivityId(execution, gateway.Id);

        int nbrOfExecutionsToJoin = gateway.IncomingFlows?.Count ?? 0;
        int nbrOfExecutionsCurrentlyJoined = joinedExecutions.Count;

        if (nbrOfExecutionsCurrentlyJoined == nbrOfExecutionsToJoin)
        {
            if (nbrOfExecutionsToJoin > 1)
            {
                foreach (var joinedExecution in joinedExecutions)
                {
                    if (joinedExecution.Id != execution.Id)
                    {
                        joinedExecution.IsActive = false;
                        joinedExecution.IsEnded = true;
                    }
                }
            }

            execution.IsConcurrent = false;
            execution.IsScope = true;

            Agenda?.PlanTakeOutgoingSequenceFlowsOperation(execution, false);
        }

        return Task.CompletedTask;
    }

    private List<ExecutionEntity> FindInactiveExecutionsByActivityId(ExecutionEntity execution, string activityId)
    {
        var result = new List<ExecutionEntity>();
        var root = execution;
        while (root.Parent != null)
            root = root.Parent;
        CollectInactiveExecutionsByActivityId(root, activityId, result);
        return result;
    }

    private void CollectInactiveExecutionsByActivityId(ExecutionEntity execution, string activityId, List<ExecutionEntity> result)
    {
        if (!execution.IsActive && !execution.IsEnded && execution.ActivityId == activityId)
            result.Add(execution);

        foreach (var child in execution.ChildExecutions)
            CollectInactiveExecutionsByActivityId(child, activityId, result);
    }
}

public class InclusiveGatewayActivityBehavior : GatewayActivityBehavior, IInactiveActivityBehavior
{
    private readonly IExpressionManager? _expressionManager;

    public InclusiveGatewayActivityBehavior(IExpressionManager? expressionManager = null)
    {
        _expressionManager = expressionManager;
    }

    public override Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.IsActive = false;
        return ExecuteInclusiveGatewayLogicAsync(execution, cancellationToken);
    }

    public Task ExecuteInactiveAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        return ExecuteInclusiveGatewayLogicAsync(execution, cancellationToken);
    }

    private Task ExecuteInclusiveGatewayLogicAsync(ExecutionEntity execution, CancellationToken cancellationToken)
    {
        if (execution.CurrentFlowElement is not BpmnModelNs.InclusiveGateway gateway)
            return Task.CompletedTask;

        var allExecutions = FindAllExecutions(execution);
        bool oneExecutionCanReachGateway = false;

        foreach (var executionEntity in allExecutions)
        {
            if (executionEntity.ActivityId != gateway.Id)
            {
                if (IsSameExecutionPath(execution, executionEntity) &&
                    IsReachable(execution, executionEntity.ActivityId, gateway.Id))
                {
                    oneExecutionCanReachGateway = true;
                    break;
                }
            }
            else if (executionEntity.Id == execution.Id && executionEntity.IsActive)
            {
                oneExecutionCanReachGateway = true;
                break;
            }
        }

        if (!oneExecutionCanReachGateway)
        {
            var executionsInGateway = FindInactiveExecutionsByActivityId(execution, gateway.Id);
            foreach (var executionInGateway in executionsInGateway)
            {
                if (executionInGateway.Id != execution.Id &&
                    executionInGateway.ParentId == execution.ParentId)
                {
                    executionInGateway.IsActive = false;
                    executionInGateway.IsEnded = true;
                }
            }

            execution.IsConcurrent = false;
            execution.IsScope = true;

            Agenda?.PlanTakeOutgoingSequenceFlowsOperation(execution, true);
        }

        return Task.CompletedTask;
    }

    private bool IsSameExecutionPath(ExecutionEntity gatewayExecution, ExecutionEntity activeExecution)
    {
        return activeExecution.ParentId == gatewayExecution.ParentId;
    }

    private bool IsReachable(ExecutionEntity execution, string? sourceActivityId, string targetActivityId)
    {
        if (sourceActivityId == null || execution.Process == null) return false;
        return IsReachableInProcess(execution.Process, sourceActivityId, targetActivityId, new HashSet<string>());
    }

    private bool IsReachableInProcess(BpmnModelNs.Process process, string sourceActivityId, string targetActivityId, HashSet<string> visited)
    {
        if (sourceActivityId == targetActivityId) return true;
        if (!visited.Add(sourceActivityId)) return false;

        var sourceElement = process.FlowElements.FirstOrDefault(e => e.Id == sourceActivityId) as BpmnModelNs.FlowNode;
        if (sourceElement == null) return false;

        if (sourceElement.OutgoingFlows == null) return false;

        foreach (var outgoingFlow in sourceElement.OutgoingFlows)
        {
            if (outgoingFlow.TargetRef == targetActivityId) return true;
            if (IsReachableInProcess(process, outgoingFlow.TargetRef, targetActivityId, visited)) return true;
        }

        return false;
    }

    private List<ExecutionEntity> FindAllExecutions(ExecutionEntity execution)
    {
        var result = new List<ExecutionEntity>();
        var root = execution;
        while (root.Parent != null)
            root = root.Parent;
        CollectAllExecutions(root, result);
        return result;
    }

    private void CollectAllExecutions(ExecutionEntity execution, List<ExecutionEntity> result)
    {
        result.Add(execution);
        foreach (var child in execution.ChildExecutions)
            CollectAllExecutions(child, result);
    }

    private List<ExecutionEntity> FindInactiveExecutionsByActivityId(ExecutionEntity execution, string activityId)
    {
        var result = new List<ExecutionEntity>();
        var root = execution;
        while (root.Parent != null)
            root = root.Parent;
        CollectInactiveExecutionsByActivityId(root, activityId, result);
        return result;
    }

    private void CollectInactiveExecutionsByActivityId(ExecutionEntity execution, string activityId, List<ExecutionEntity> result)
    {
        if (!execution.IsActive && !execution.IsEnded && execution.ActivityId == activityId)
            result.Add(execution);

        foreach (var child in execution.ChildExecutions)
            CollectInactiveExecutionsByActivityId(child, activityId, result);
    }
}

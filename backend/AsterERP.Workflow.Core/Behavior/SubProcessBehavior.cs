using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Helper;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class SubProcessActivityBehavior : FlowNodeActivityBehavior
{
    public BpmnModelNs.SubProcess? SubProcess { get; set; }
    public bool IsTransaction { get; set; }
    public bool IsAdhoc { get; set; }
    public string? CompletionCondition { get; set; }

    protected IExpressionManager? ExpressionManager { get; set; }

    public SubProcessActivityBehavior() { }

    public SubProcessActivityBehavior(
        BpmnModelNs.SubProcess subProcess,
        IExpressionManager? expressionManager = null)
    {
        SubProcess = subProcess;
        ExpressionManager = expressionManager;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (SubProcess == null)
        {
            await LeaveAsync(execution, cancellationToken);
            return;
        }

        var startEvent = FindStartEvent(SubProcess);
        if (startEvent == null)
        {
            throw new WorkflowEngineException($"SubProcess '{SubProcess.Id}' has no start event");
        }

        execution.IsScope = true;
        execution.SetVariableLocal("_subProcessId", SubProcess.Id);
        execution.SetVariableLocal("_subProcessStarted", true);

        var dataObjectVars = ProcessDataObjects(SubProcess);
        if (dataObjectVars.Count > 0)
        {
            foreach (var kv in dataObjectVars)
            {
                execution.SetVariableLocal(kv.Key, kv.Value);
            }
        }

        var childExecution = CreateChildExecution(execution, startEvent);

        if (SubProcess.FlowElements != null && SubProcess.FlowElements.Count > 0)
        {
            await ExecuteFlowElementsAsync(childExecution, SubProcess, startEvent, cancellationToken);
        }
        else
        {
            await LeaveAsync(execution, cancellationToken);
        }
    }

    public virtual ExecutionEntity CreateChildExecution(ExecutionEntity parentExecution, BpmnModelNs.FlowElement startElement)
    {
        var childExecution = new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid(),
            ProcessInstanceId = parentExecution.ProcessInstanceId,
            ProcessDefinitionId = parentExecution.ProcessDefinitionId,
            ParentId = parentExecution.Id,
            Parent = parentExecution,
            IsActive = true,
            IsScope = false,
            IsConcurrent = false,
            IsProcessInstanceType = false,
            CurrentFlowElement = startElement,
            CurrentFlowElementId = startElement.Id,
            ActivityId = startElement.Id,
            TenantId = parentExecution.TenantId,
            BusinessKey = parentExecution.BusinessKey,
            Process = parentExecution.Process,
            Variables = new Dictionary<string, object?>()
        };
        parentExecution.ChildExecutions.Add(childExecution);
        return childExecution;
    }

    public virtual Dictionary<string, object?> ProcessDataObjects(BpmnModelNs.SubProcess subProcess)
    {
        var variablesMap = new Dictionary<string, object?>();
        if (subProcess.DataObjects != null)
        {
            foreach (var dataObject in subProcess.DataObjects)
            {
                if (dataObject is BpmnModelNs.ValuedDataObject valuedDataObject)
                {
                    variablesMap[valuedDataObject.Name ?? valuedDataObject.Id] = valuedDataObject.Value;
                }
            }
        }
        return variablesMap;
    }

    protected virtual BpmnModelNs.StartEvent? FindStartEvent(BpmnModelNs.SubProcess subProcess)
    {
        return subProcess.FlowElements.OfType<BpmnModelNs.StartEvent>().FirstOrDefault();
    }

    protected virtual List<BpmnModelNs.SequenceFlow> GetSequenceFlows(BpmnModelNs.SubProcess subProcess)
    {
        return subProcess.FlowElements.OfType<BpmnModelNs.SequenceFlow>().ToList();
    }

    protected virtual async Task ExecuteFlowElementsAsync(
        ExecutionEntity execution,
        BpmnModelNs.SubProcess subProcess,
        BpmnModelNs.FlowElement startElement,
        CancellationToken cancellationToken)
    {
        var currentElement = startElement;
        var endEvents = new List<BpmnModelNs.EndEvent>();

        while (currentElement != null && execution.IsActive && !execution.IsEnded)
        {
            var nextFlowElement = await ExecuteSingleFlowElementAsync(
                execution,
                subProcess,
                currentElement,
                endEvents,
                cancellationToken);

            if (nextFlowElement == null)
            {
                break;
            }

            currentElement = nextFlowElement;
        }

        if (execution.IsActive && !execution.IsEnded)
        {
            await LeaveAsync(execution, cancellationToken);
        }
    }

    protected virtual async Task<BpmnModelNs.FlowElement?> ExecuteSingleFlowElementAsync(
        ExecutionEntity execution,
        BpmnModelNs.SubProcess subProcess,
        BpmnModelNs.FlowElement currentElement,
        List<BpmnModelNs.EndEvent> endEvents,
        CancellationToken cancellationToken)
    {
        execution.CurrentFlowElement = currentElement;
        execution.CurrentFlowElementId = currentElement.Id;
        execution.ActivityId = currentElement.Id;

        switch (currentElement)
        {
            case BpmnModelNs.StartEvent startEvent:
                var startBehavior = CreateStartEventBehavior(startEvent);
                if (startBehavior != null)
                {
                    await startBehavior.ExecuteAsync(execution, cancellationToken);
                }
                return FindNextElement(execution, subProcess, currentElement);

            case BpmnModelNs.EndEvent endEvent:
                endEvents.Add(endEvent);
                execution.IsActive = false;
                execution.IsEnded = true;

                if (execution.Parent != null && !execution.Parent.IsEnded)
                {
                    await CompletingAsync(execution.Parent, cancellationToken);
                    await CompletedAsync(execution.Parent, cancellationToken);
                }
                return null;

            case BpmnModelNs.UserTask userTask:
                var userTaskBehavior = CreateUserTaskBehavior(userTask);
                if (userTaskBehavior != null)
                {
                    await userTaskBehavior.ExecuteAsync(execution, cancellationToken);
                }
                return null;

            case BpmnModelNs.ServiceTask serviceTask:
                var serviceTaskBehavior = CreateServiceTaskBehavior(serviceTask);
                if (serviceTaskBehavior != null)
                {
                    await serviceTaskBehavior.ExecuteAsync(execution, cancellationToken);
                }
                return FindNextElement(execution, subProcess, currentElement);

            case BpmnModelNs.ScriptTask scriptTask:
                var scriptTaskBehavior = CreateScriptTaskBehavior(scriptTask);
                if (scriptTaskBehavior != null)
                {
                    await scriptTaskBehavior.ExecuteAsync(execution, cancellationToken);
                }
                return FindNextElement(execution, subProcess, currentElement);

            case BpmnModelNs.ExclusiveGateway exclusiveGw:
                var exclusiveBehavior = CreateExclusiveGatewayBehavior(exclusiveGw);
                if (exclusiveBehavior != null)
                {
                    await exclusiveBehavior.ExecuteAsync(execution, cancellationToken);
                }
                return FindNextElement(execution, subProcess, currentElement);

            case BpmnModelNs.ParallelGateway parallelGw:
                var parallelBehavior = CreateParallelGatewayBehavior(parallelGw);
                if (parallelBehavior != null)
                {
                    await parallelBehavior.ExecuteAsync(execution, cancellationToken);
                }
                return FindNextElement(execution, subProcess, currentElement);

            case BpmnModelNs.InclusiveGateway inclusiveGw:
                var inclusiveBehavior = CreateInclusiveGatewayBehavior(inclusiveGw);
                if (inclusiveBehavior != null)
                {
                    await inclusiveBehavior.ExecuteAsync(execution, cancellationToken);
                }
                return FindNextElement(execution, subProcess, currentElement);

            case BpmnModelNs.SubProcess nestedSubProcess:
                var nestedBehavior = new EmbeddedSubProcessActivityBehavior(nestedSubProcess, ExpressionManager);
                await nestedBehavior.ExecuteAsync(execution, cancellationToken);
                return FindNextElement(execution, subProcess, currentElement);

            default:
                return FindNextElement(execution, subProcess, currentElement);
        }
    }

    protected virtual BpmnModelNs.FlowElement? FindNextElement(
        ExecutionEntity execution,
        BpmnModelNs.SubProcess subProcess,
        BpmnModelNs.FlowElement currentElement)
    {
        var sequenceFlows = GetSequenceFlows(subProcess);
        var outgoingFlows = sequenceFlows
            .Where(sf => sf.SourceRef == currentElement.Id)
            .ToList();

        if (outgoingFlows.Count == 0)
        {
            return null;
        }

        if (outgoingFlows.Count == 1)
        {
            var targetRef = outgoingFlows[0].TargetRef;
            return subProcess.FlowElements.FirstOrDefault(fe => fe.Id == targetRef);
        }

        var nextElement = subProcess.FlowElements.FirstOrDefault(fe => fe.Id == outgoingFlows[0].TargetRef);

        if (currentElement is BpmnModelNs.ExclusiveGateway exclusiveGw)
        {
            foreach (var flow in outgoingFlows)
            {
                var sequenceFlow = sequenceFlows.FirstOrDefault(sf => sf.Id == flow.Id);
                if (sequenceFlow?.ConditionExpression != null && ExpressionManager != null)
                {
                    var result = ExpressionManager.Evaluate(sequenceFlow.ConditionExpression, execution.Variables);
                    if (result is bool boolResult && boolResult)
                    {
                        return subProcess.FlowElements.FirstOrDefault(fe => fe.Id == flow.TargetRef);
                    }
                }
            }

            var defaultFlowId = exclusiveGw.DefaultFlow;
            if (!string.IsNullOrEmpty(defaultFlowId))
            {
                return subProcess.FlowElements.FirstOrDefault(fe => fe.Id == defaultFlowId);
            }

            var defaultFlow = sequenceFlows.FirstOrDefault(sf => sf.SourceRef == currentElement.Id);
            if (defaultFlow != null)
            {
                return subProcess.FlowElements.FirstOrDefault(fe => fe.Id == defaultFlow.TargetRef);
            }
        }

        return nextElement;
    }

    protected virtual int GetExpectedEndEvents(BpmnModelNs.SubProcess subProcess)
    {
        return subProcess.FlowElements.Count(fe => fe is BpmnModelNs.EndEvent);
    }

    protected virtual IBpmnActivityBehavior? CreateStartEventBehavior(BpmnModelNs.StartEvent startEvent)
    {
        return new StartEventActivityBehavior();
    }

    protected virtual IBpmnActivityBehavior? CreateUserTaskBehavior(BpmnModelNs.UserTask userTask)
    {
        return new UserTaskActivityBehavior(userTask, ExpressionManager);
    }

    protected virtual IBpmnActivityBehavior? CreateServiceTaskBehavior(BpmnModelNs.ServiceTask serviceTask)
    {
        return new ServiceTaskActivityBehavior();
    }

    protected virtual IBpmnActivityBehavior? CreateScriptTaskBehavior(BpmnModelNs.ScriptTask scriptTask)
    {
        return new ScriptTaskActivityBehavior(
            scriptTask.Script,
            scriptTask.ScriptFormat,
            scriptTask.AutoStoreVariables,
            scriptTask.ResultVariable,
            ExpressionManager);
    }

    protected virtual IBpmnActivityBehavior? CreateExclusiveGatewayBehavior(BpmnModelNs.ExclusiveGateway exclusiveGw)
    {
        return new ExclusiveGatewayActivityBehavior();
    }

    protected virtual IBpmnActivityBehavior? CreateParallelGatewayBehavior(BpmnModelNs.ParallelGateway parallelGw)
    {
        return new ParallelGatewayActivityBehavior();
    }

    protected virtual IBpmnActivityBehavior? CreateInclusiveGatewayBehavior(BpmnModelNs.InclusiveGateway inclusiveGw)
    {
        return new InclusiveGatewayActivityBehavior();
    }

    public virtual async Task CompletingAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        PropagateVariablesToParent(execution);

        CleanupChildExecutions(execution);

        await Task.CompletedTask;
    }

    public virtual async Task CompletedAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (execution.CurrentFlowElement != null)
        {
            var listenerHelper = new ListenerNotificationHelper(ExpressionManager);
            await listenerHelper.ExecuteExecutionListeners(
                execution.CurrentFlowElement,
                execution,
                "end",
                cancellationToken);
        }

        await LeaveAsync(execution, cancellationToken);
    }

    public virtual void PropagateVariablesToParent(ExecutionEntity execution)
    {
        if (execution.Parent == null) return;

        var snapshotter = new SubProcessVariableSnapshotter();
        var childVars = execution.Variables
            .Where(kv => !kv.Key.StartsWith("_") && !IsLoopVariable(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        foreach (var kv in childVars)
        {
            if (kv.Value != null && !execution.Parent.Variables.ContainsKey(kv.Key))
            {
                execution.Parent.SetVariable(kv.Key, kv.Value);
            }
        }
    }

    protected virtual void CleanupChildExecutions(ExecutionEntity execution)
    {
        foreach (var childExecution in execution.ChildExecutions.ToList())
        {
            CleanupChildExecutions(childExecution);
            childExecution.IsActive = false;
            childExecution.IsEnded = true;
        }
        execution.ChildExecutions.Clear();
    }

    protected virtual bool IsLoopVariable(string variableName)
    {
        return variableName == MultiInstanceActivityBehavior.NumberOfInstances
            || variableName == MultiInstanceActivityBehavior.NumberOfActiveInstances
            || variableName == MultiInstanceActivityBehavior.NumberOfCompletedInstances
            || variableName == "loopCounter";
    }
}

public class EmbeddedSubProcessActivityBehavior : SubProcessActivityBehavior
{
    public EmbeddedSubProcessActivityBehavior() { }

    public EmbeddedSubProcessActivityBehavior(
        BpmnModelNs.SubProcess subProcess,
        IExpressionManager? expressionManager = null)
        : base(subProcess, expressionManager)
    {
    }
}

public class EventSubProcessActivityBehavior : SubProcessActivityBehavior
{
    public EventSubProcessActivityBehavior() { }

    public EventSubProcessActivityBehavior(
        BpmnModelNs.SubProcess subProcess,
        IExpressionManager? expressionManager = null)
        : base(subProcess, expressionManager)
    {
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (SubProcess == null)
        {
            await LeaveAsync(execution, cancellationToken);
            return;
        }

        execution.SetVariableLocal("_eventSubProcessId", SubProcess.Id);
        execution.SetVariableLocal("_eventSubProcessStarted", true);

        var startEvent = FindStartEvent(SubProcess);
        if (startEvent != null && SubProcess.FlowElements != null && SubProcess.FlowElements.Count > 0)
        {
            await ExecuteFlowElementsAsync(execution, SubProcess, startEvent, cancellationToken);
        }
        else
        {
            await LeaveAsync(execution, cancellationToken);
        }
    }
}

public class TransactionSubProcessActivityBehavior : SubProcessActivityBehavior
{
    public bool CompensationTriggered { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsCompensating { get; set; }

    public TransactionSubProcessActivityBehavior()
    {
        IsTransaction = true;
    }

    public TransactionSubProcessActivityBehavior(
        BpmnModelNs.SubProcess subProcess,
        IExpressionManager? expressionManager = null)
        : base(subProcess, expressionManager)
    {
        IsTransaction = true;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_transactionStarted", true);
        execution.SetVariableLocal("_transactionId", AbpTimeIdProvider.NewGuid());
        execution.SetVariableLocal("_transactionActive", true);
        execution.IsScope = true;

        try
        {
            await base.ExecuteAsync(execution, cancellationToken);

            if (execution.IsEnded || !execution.IsActive)
            {
                execution.SetVariableLocal("_transactionCompleted", true);
                execution.SetVariableLocal("_transactionActive", false);
            }
        }
        catch (Exception)
        {
            await RollbackAsync(execution, cancellationToken);
            throw;
        }
    }

    public virtual async Task TriggerCompensationAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        CompensationTriggered = true;
        IsCompensating = true;

        var transactionStarted = execution.GetVariable("_transactionStarted");
        if (transactionStarted is bool started && started)
        {
            await RollbackAsync(execution, cancellationToken);
        }
    }

    public virtual async Task CancelAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        IsCancelled = true;
        execution.SetVariableLocal("_transactionCancelled", true);
        execution.SetVariableLocal("_transactionActive", false);

        await TriggerCompensationAsync(execution, cancellationToken);

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    public virtual async Task CompensateAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_transactionCompensating", true);
        await TriggerCompensationAsync(execution, cancellationToken);
    }

    public virtual async Task CompleteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_transactionCompleted", true);
        execution.SetVariableLocal("_transactionActive", false);
        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    protected virtual async Task RollbackAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_transactionRolledBack", true);
        execution.SetVariableLocal("_transactionActive", false);

        var compensationActivities = SubProcess?.FlowElements
            .Where(fe => fe is BpmnModelNs.Activity activity && activity.IsForCompensation)
            .ToList();

        if (compensationActivities != null && compensationActivities.Count > 0)
        {
            foreach (var activity in compensationActivities)
            {
                execution.CurrentFlowElement = activity;
                execution.CurrentFlowElementId = activity.Id;
                execution.ActivityId = activity.Id;

                var compensationBehavior = new CompensationEventActivityBehavior();
                await compensationBehavior.ExecuteAsync(execution, cancellationToken);
            }
        }
    }

    protected virtual async Task PropagateErrorAsync(ExecutionEntity execution, string errorCode, CancellationToken cancellationToken = default)
    {
        try
        {
            ErrorPropagation.PropagateError(errorCode, execution);
        }
        catch
        {
            await CancelAsync(execution, cancellationToken);
        }
    }
}

public class AdhocSubProcessActivityBehavior : SubProcessActivityBehavior
{
    public bool Ordering { get; set; }
    public string? RemainingActivitiesExpression { get; set; }

    public AdhocSubProcessActivityBehavior()
    {
        IsAdhoc = true;
    }

    public AdhocSubProcessActivityBehavior(
        BpmnModelNs.SubProcess subProcess,
        IExpressionManager? expressionManager = null)
        : base(subProcess, expressionManager)
    {
        IsAdhoc = true;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (SubProcess == null)
        {
            await LeaveAsync(execution, cancellationToken);
            return;
        }

        execution.IsScope = true;
        execution.SetVariableLocal("_adhocSubProcessId", SubProcess.Id);
        execution.SetVariableLocal("_adhocSubProcessStarted", true);

        var dataObjectVars = ProcessDataObjects(SubProcess);
        if (dataObjectVars.Count > 0)
        {
            foreach (var kv in dataObjectVars)
            {
                execution.SetVariableLocal(kv.Key, kv.Value);
            }
        }

        var activities = SubProcess.FlowElements
            .Where(fe => fe is BpmnModelNs.UserTask or BpmnModelNs.ServiceTask or BpmnModelNs.ScriptTask)
            .ToList();

        foreach (var activity in activities)
        {
            execution.CurrentFlowElement = activity;
            execution.CurrentFlowElementId = activity.Id;
            execution.ActivityId = activity.Id;

            switch (activity)
            {
                case BpmnModelNs.UserTask userTask:
                    var userTaskBehavior = new UserTaskActivityBehavior(userTask, ExpressionManager);
                    await userTaskBehavior.ExecuteAsync(execution, cancellationToken);
                    break;

                case BpmnModelNs.ServiceTask:
                    var serviceTaskBehavior = new ServiceTaskActivityBehavior();
                    await serviceTaskBehavior.ExecuteAsync(execution, cancellationToken);
                    break;

                case BpmnModelNs.ScriptTask scriptTask:
                    var scriptTaskBehavior = new ScriptTaskActivityBehavior(
                        scriptTask.Script,
                        scriptTask.ScriptFormat,
                        scriptTask.AutoStoreVariables,
                        scriptTask.ResultVariable,
                        ExpressionManager);
                    await scriptTaskBehavior.ExecuteAsync(execution, cancellationToken);
                    break;
            }

            if (!string.IsNullOrEmpty(CompletionCondition) && ExpressionManager != null)
            {
                var result = ExpressionManager.Evaluate(CompletionCondition, execution.Variables);
                if (result is bool boolResult && boolResult)
                {
                    break;
                }
            }
        }

        await LeaveAsync(execution, cancellationToken);
    }
}



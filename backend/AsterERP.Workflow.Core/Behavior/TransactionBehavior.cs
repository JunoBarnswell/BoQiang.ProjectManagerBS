using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Helper;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class TransactionActivityBehavior : SubProcessActivityBehavior
{
    public bool IsCancelled { get; set; }
    public bool IsCompensating { get; set; }
    public bool IsCompleted { get; set; }

    public TransactionActivityBehavior()
    {
        IsTransaction = true;
    }

    public TransactionActivityBehavior(
        BpmnModelNs.SubProcess subProcess) : base(subProcess)
    {
        IsTransaction = true;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariable("_transactionActive", true);
        execution.SetVariableLocal("_transactionStarted", true);
        execution.SetVariableLocal("_transactionId", AbpTimeIdProvider.NewGuid());
        execution.IsScope = true;

        if (SubProcess != null)
        {
            var dataObjectVars = ProcessDataObjects(SubProcess);
            if (dataObjectVars.Count > 0)
            {
                foreach (var kv in dataObjectVars)
                {
                    execution.SetVariableLocal(kv.Key, kv.Value);
                }
            }
        }

        var childExecution = new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid(),
            ProcessInstanceId = execution.ProcessInstanceId,
            ProcessDefinitionId = execution.ProcessDefinitionId,
            Parent = execution,
            IsActive = true,
            IsScope = true,
            CurrentFlowElement = SubProcess,
            CurrentFlowElementId = SubProcess?.Id,
            TenantId = execution.TenantId
        };
        execution.ChildExecutions.Add(childExecution);

        if (SubProcess != null)
        {
            var startEvents = SubProcess.FlowElements.OfType<BpmnModelNs.StartEvent>();
            foreach (var startEvent in startEvents)
            {
                var startExecution = new ExecutionEntity
                {
                    Id = AbpTimeIdProvider.NewGuid(),
                    ProcessInstanceId = execution.ProcessInstanceId,
                    ProcessDefinitionId = execution.ProcessDefinitionId,
                    Parent = childExecution,
                    IsActive = true,
                    CurrentFlowElement = startEvent,
                    CurrentFlowElementId = startEvent.Id,
                    TenantId = execution.TenantId
                };
                childExecution.ChildExecutions.Add(startExecution);
            }
        }
    }

    public virtual async Task CancelAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        IsCancelled = true;
        execution.SetVariable("_transactionCancelled", true);
        execution.SetVariable("_transactionActive", false);

        await ExecuteCompensationBoundaryEventsAsync(execution, cancellationToken);

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    public virtual async Task CompensateAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        IsCompensating = true;
        execution.SetVariable("_transactionCompensating", true);

        await ExecuteCompensationActivitiesAsync(execution, cancellationToken);
    }

    public virtual async Task CompleteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        IsCompleted = true;
        execution.SetVariable("_transactionCompleted", true);
        execution.SetVariable("_transactionActive", false);
        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    protected virtual async Task ExecuteCompensationBoundaryEventsAsync(
        ExecutionEntity execution, CancellationToken cancellationToken)
    {
        if (SubProcess == null) return;

        var boundaryEvents = SubProcess.FlowElements
            .OfType<BpmnModelNs.BoundaryEvent>()
            .Where(be => be.EventDefinitions?.Any(ed => ed is BpmnModelNs.CompensateEventDefinition) == true)
            .ToList();

        foreach (var boundaryEvent in boundaryEvents)
        {
            var childExecution = new ExecutionEntity
            {
                Id = AbpTimeIdProvider.NewGuid(),
                ProcessInstanceId = execution.ProcessInstanceId,
                ProcessDefinitionId = execution.ProcessDefinitionId,
                ParentId = execution.Id,
                Parent = execution,
                IsActive = true,
                IsScope = false,
                IsConcurrent = true,
                CurrentFlowElement = boundaryEvent,
                CurrentFlowElementId = boundaryEvent.Id,
                ActivityId = boundaryEvent.Id,
                TenantId = execution.TenantId,
                Variables = new Dictionary<string, object?>()
            };
            execution.ChildExecutions.Add(childExecution);

            var compensateDef = boundaryEvent.EventDefinitions?
                .OfType<BpmnModelNs.CompensateEventDefinition>()
                .FirstOrDefault();

            if (compensateDef != null)
            {
                var compensationBehavior = new CompensateBoundaryEventActivityBehavior(compensateDef, true);
                await compensationBehavior.ExecuteAsync(childExecution, cancellationToken);
            }
        }
    }

    protected virtual async Task ExecuteCompensationActivitiesAsync(
        ExecutionEntity execution, CancellationToken cancellationToken)
    {
        if (SubProcess == null) return;

        var compensationActivities = SubProcess.FlowElements
            .Where(fe => fe is BpmnModelNs.Activity activity && activity.IsForCompensation)
            .Cast<BpmnModelNs.Activity>()
            .ToList();

        foreach (var activity in compensationActivities)
        {
            execution.CurrentFlowElement = activity;
            execution.CurrentFlowElementId = activity.Id;
            execution.ActivityId = activity.Id;

            var compensationBehavior = new CompensationEventActivityBehavior();
            await compensationBehavior.ExecuteAsync(execution, cancellationToken);
        }
    }

    public virtual async Task PropagateErrorAsync(ExecutionEntity execution, string errorCode, CancellationToken cancellationToken = default)
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

    public virtual async Task LastExecutionEndedAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        await LeaveAsync(execution, cancellationToken);
    }
}



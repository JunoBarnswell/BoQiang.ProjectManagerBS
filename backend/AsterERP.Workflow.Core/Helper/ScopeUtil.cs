using System;
using System.Collections.Generic;
using System.Linq;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Helper;

public static class ScopeUtil
{
    public static void ThrowCompensationEvent(
        List<CompensateEventSubscription> eventSubscriptions,
        ExecutionEntity execution,
        bool async)
    {
        var sortedSubscriptions = eventSubscriptions
            .OrderByDescending(s => s.Created)
            .ToList();

        foreach (var subscription in sortedSubscriptions)
        {
            var compensatingExecution = new ExecutionEntity
            {
                Id = AbpTimeIdProvider.NewGuid(),
                ProcessInstanceId = execution.ProcessInstanceId,
                ProcessDefinitionId = execution.ProcessDefinitionId,
                Parent = execution,
                ParentId = execution.Id,
                IsActive = true,
                IsEnded = false,
                IsScope = false,
                IsConcurrent = true,
                IsProcessInstanceType = false,
                Process = execution.Process,
                CurrentFlowElement = execution.Process?.FlowElements.Find(e => e.Id == subscription.ActivityId) as BpmnModelNs.FlowNode,
                Variables = new Dictionary<string, object?>(execution.Variables)
            };
            execution.ChildExecutions.Add(compensatingExecution);
        }
    }

    public static void CreateCopyOfSubProcessExecutionForCompensation(ExecutionEntity subProcessExecution)
    {
        if (subProcessExecution.Process == null) return;

        var processInstanceExecution = FindProcessInstanceExecution(subProcessExecution);

        var eventScopeExecution = new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid(),
            ProcessInstanceId = subProcessExecution.ProcessInstanceId,
            ProcessDefinitionId = subProcessExecution.ProcessDefinitionId,
            Parent = processInstanceExecution,
            ParentId = processInstanceExecution?.Id,
            IsActive = false,
            IsEnded = false,
            IsScope = true,
            IsConcurrent = false,
            IsProcessInstanceType = false,
            Process = subProcessExecution.Process,
            CurrentFlowElement = subProcessExecution.CurrentFlowElement,
            Variables = new SubProcessVariableSnapshotter().CreateSnapshot(subProcessExecution)
        };

        if (processInstanceExecution != null)
        {
            processInstanceExecution.ChildExecutions.Add(eventScopeExecution);
        }
    }

    private static ExecutionEntity? FindProcessInstanceExecution(ExecutionEntity execution)
    {
        var current = execution;
        while (current != null)
        {
            if (current.IsProcessInstanceType) return current;
            current = current.Parent;
        }
        return execution;
    }
}

public class CompensateEventSubscription
{
    public string? Id { get; set; }
    public string? ExecutionId { get; set; }
    public string? ActivityId { get; set; }
    public string? EventType { get; set; } = "compensate";
    public string? Configuration { get; set; }
    public DateTime Created { get; set; } = AbpTimeIdProvider.UtcNow;
    public string? ProcessInstanceId { get; set; }
}



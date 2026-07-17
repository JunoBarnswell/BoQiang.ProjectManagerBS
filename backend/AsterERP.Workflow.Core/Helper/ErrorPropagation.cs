using System;
using System.Collections.Generic;
using System.Linq;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Helper;

public static class ErrorPropagation
{
    public static void PropagateError(string errorRef, ExecutionEntity execution)
    {
        var isCatchExecutedForProcess = false;

        try
        {
            var eventMap = FindCatchingEventsForProcess(execution, errorRef);

            if (eventMap.Count > 0)
            {
                isCatchExecutedForProcess = ExecuteCatch(eventMap, execution, errorRef);
            }
        }
        finally
        {
            if (!isCatchExecutedForProcess)
            {
                throw new Common.WorkflowEngineException(
                    $"No catching boundary event or event sub-process found for error with errorCode '{errorRef}'");
            }
        }
    }

    private static bool ExecuteCatch(
        Dictionary<string, List<BpmnModelNs.Event>> eventMap,
        ExecutionEntity currentExecution,
        string errorRef)
    {
        BpmnModelNs.Event? matchingEvent = null;
        ExecutionEntity? parentExecution = null;

        if (eventMap.ContainsKey(currentExecution.CurrentActivityId ?? ""))
        {
            matchingEvent = eventMap[currentExecution.CurrentActivityId!].First();
            parentExecution = currentExecution.Parent ?? currentExecution;
        }
        else
        {
            parentExecution = currentExecution.Parent;

            while (matchingEvent == null && parentExecution != null)
            {
                if (eventMap.ContainsKey(parentExecution.CurrentActivityId ?? ""))
                {
                    matchingEvent = eventMap[parentExecution.CurrentActivityId!].First();
                }
                else
                {
                    parentExecution = parentExecution.Parent;
                }
            }
        }

        if (matchingEvent != null && parentExecution != null)
        {
            ExecuteEventHandler(matchingEvent, parentExecution, currentExecution, errorRef);
            return true;
        }

        return false;
    }

    private static void ExecuteEventHandler(
        BpmnModelNs.Event @event,
        ExecutionEntity parentExecution,
        ExecutionEntity currentExecution,
        string errorRef)
    {
        if (@event is BpmnModelNs.StartEvent startEvent)
        {
            currentExecution.IsEnded = true;
            currentExecution.IsActive = false;

            var eventSubProcessExecution = new ExecutionEntity
            {
                Id = AbpTimeIdProvider.NewGuid(),
                ProcessInstanceId = parentExecution.ProcessInstanceId,
                ProcessDefinitionId = parentExecution.ProcessDefinitionId,
                Parent = parentExecution,
                ParentId = parentExecution.Id,
                IsActive = true,
                IsEnded = false,
                IsScope = true,
                IsConcurrent = false,
                IsProcessInstanceType = false,
                Process = parentExecution.Process,
                CurrentFlowElement = @event,
                Variables = new Dictionary<string, object?>(parentExecution.Variables)
            };
            parentExecution.ChildExecutions.Add(eventSubProcessExecution);
        }
        else if (@event is BpmnModelNs.BoundaryEvent)
        {
            var boundaryExecution = parentExecution.ChildExecutions
                .FirstOrDefault(e => e.CurrentActivityId == @event.Id);

            if (boundaryExecution != null)
            {
                boundaryExecution.SetVariable("_errorTriggered", true);
                boundaryExecution.SetVariable("_errorCode", errorRef);
            }
        }
    }

    private static Dictionary<string, List<BpmnModelNs.Event>> FindCatchingEventsForProcess(
        ExecutionEntity execution,
        string errorRef)
    {
        var eventMap = new Dictionary<string, List<BpmnModelNs.Event>>();

        var process = execution.Process;
        if (process == null) return eventMap;

        FindCatchingBoundaryEvents(process, errorRef, eventMap);
        FindCatchingEventSubprocesses(process, errorRef, eventMap);

        return eventMap;
    }

    private static void FindCatchingBoundaryEvents(
        BpmnModelNs.Process process,
        string errorRef,
        Dictionary<string, List<BpmnModelNs.Event>> eventMap)
    {
        foreach (var flowElement in process.FlowElements)
        {
            if (flowElement is BpmnModelNs.BoundaryEvent boundaryEvent &&
                boundaryEvent.AttachedToRefId != null)
            {
                var errorDef = boundaryEvent.EventDefinitions?.OfType<BpmnModelNs.ErrorEventDefinition>().FirstOrDefault();
                if (errorDef != null)
                {
                    var eventErrorCode = errorDef.ErrorCode;
                    if (IsErrorCodeMatching(eventErrorCode, errorRef))
                    {
                        if (!eventMap.ContainsKey(boundaryEvent.AttachedToRefId))
                        {
                            eventMap[boundaryEvent.AttachedToRefId] = new List<BpmnModelNs.Event>();
                        }
                        eventMap[boundaryEvent.AttachedToRefId].Add(boundaryEvent);
                    }
                }
            }
        }
    }

    private static void FindCatchingEventSubprocesses(
        BpmnModelNs.Process process,
        string errorRef,
        Dictionary<string, List<BpmnModelNs.Event>> eventMap)
    {
        foreach (var flowElement in process.FlowElements)
        {
            if (flowElement is BpmnModelNs.SubProcess subProcess &&
                subProcess.TriggeredByEvent)
            {
                foreach (var childElement in subProcess.FlowElements)
                {
                    if (childElement is BpmnModelNs.StartEvent startEvent)
                    {
                        var errorDef = startEvent.EventDefinitions?.OfType<BpmnModelNs.ErrorEventDefinition>().FirstOrDefault();
                        if (errorDef != null)
                        {
                            var eventErrorCode = errorDef.ErrorCode;
                            if (IsErrorCodeMatching(eventErrorCode, errorRef))
                            {
                                if (!eventMap.ContainsKey(subProcess.Id))
                                {
                                    eventMap[subProcess.Id] = new List<BpmnModelNs.Event>();
                                }
                                eventMap[subProcess.Id].Add(startEvent);
                            }
                        }
                    }
                }
            }
        }
    }

    private static bool IsErrorCodeMatching(string? eventErrorCode, string? compareErrorCode)
    {
        if (eventErrorCode == null || compareErrorCode == null) return true;
        return eventErrorCode == compareErrorCode;
    }

    public static bool MapException(Exception e, ExecutionEntity execution, List<BpmnModelNs.MapExceptionEntry> exceptionMap)
    {
        var errorCode = FindMatchingExceptionMapping(e, exceptionMap);
        if (errorCode != null)
        {
            PropagateError(errorCode, execution);
            return true;
        }
        return false;
    }

    private static string? FindMatchingExceptionMapping(Exception e, List<BpmnModelNs.MapExceptionEntry> exceptionMap)
    {
        string? defaultExceptionMapping = null;

        foreach (var me in exceptionMap)
        {
            var exceptionClass = me.ClassName;
            var meErrorCode = me.ErrorCode;

            if (!string.IsNullOrEmpty(meErrorCode) && string.IsNullOrEmpty(exceptionClass) && defaultExceptionMapping == null)
            {
                defaultExceptionMapping = meErrorCode;
                continue;
            }

            if (string.IsNullOrEmpty(meErrorCode) || string.IsNullOrEmpty(exceptionClass)) continue;

            if (e.GetType().FullName == exceptionClass || e.GetType().Name == exceptionClass)
            {
                return meErrorCode;
            }

            if (me.AndChildren)
            {
                var classType = Type.GetType(exceptionClass);
                if (classType != null && classType.IsAssignableFrom(e.GetType()))
                {
                    return meErrorCode;
                }
            }
        }

        return defaultExceptionMapping;
    }
}



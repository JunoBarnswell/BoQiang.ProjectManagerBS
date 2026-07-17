using System.Collections.Generic;
using System.Linq;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class StartEventValidator : ProcessLevelValidator
{
    public override string ValidatorName => "StartEventValidator";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        var startEvents = FindDirectFlowElementsOfType<StartEvent>(process);

        if (startEvents.Count == 0)
        {
            AddError(errors, "START_EVENT_MISSING",
                "Process must have at least one start event", process);
        }

        ValidateMultipleStartEvents(startEvents, process, errors);
        ValidateStartEventDefinitions(startEvents, process, errors);

        foreach (var startEvent in startEvents)
        {
            if (startEvent.Id != null && GetIncomingFlows(process, startEvent.Id).Count > 0)
            {
                AddError(errors, "START_EVENT_HAS_INCOMING_FLOW",
                    "Start event must not have incoming sequence flow", process, startEvent);
            }

            if (startEvent.Id != null && GetOutgoingFlows(process, startEvent.Id).Count == 0)
            {
                AddError(errors, "START_EVENT_NO_OUTGOING_FLOW",
                    "Start event must have at least one outgoing sequence flow", process, startEvent);
            }
        }

        ValidateTimerStartEventInSubProcess(process, errors);
    }

    private void ValidateMultipleStartEvents(List<StartEvent> startEvents, Process process, List<ValidationError> errors)
    {
        var noneStartEvents = startEvents
            .Where(se => GetEventDefinitions(se).Count == 0)
            .ToList();

        if (noneStartEvents.Count > 1)
        {
            foreach (var startEvent in noneStartEvents)
            {
                AddError(errors, "START_EVENT_MULTIPLE_FOUND",
                    "Multiple none start events are not supported", process, startEvent);
            }
        }
    }

    private void ValidateStartEventDefinitions(List<StartEvent> startEvents, Process process, List<ValidationError> errors)
    {
        foreach (var startEvent in startEvents)
        {
            var eventDefinitions = GetEventDefinitions(startEvent);
            if (eventDefinitions.Count > 0)
            {
                var eventDef = eventDefinitions[0];
                if (eventDef is not TimerEventDefinition &&
                    eventDef is not SignalEventDefinition &&
                    eventDef is not MessageEventDefinition &&
                    eventDef is not ConditionalEventDefinition)
                {
                    AddError(errors, "START_EVENT_INVALID_EVENT_DEFINITION",
                        "Start event only supports timer, signal, message, or conditional event definitions", process, startEvent);
                }
            }
        }
    }

    private void ValidateTimerStartEventInSubProcess(Process process, List<ValidationError> errors)
    {
        foreach (var flowElement in process.FlowElements)
        {
            if (flowElement is SubProcess subProcess)
            {
                ValidateTimerStartEventInContainer(subProcess, process, errors);
            }
        }
    }

    private void ValidateTimerStartEventInContainer(IFlowElementsContainer container, Process process, List<ValidationError> errors)
    {
        foreach (var flowElement in container.FlowElements)
        {
            if (flowElement is StartEvent startEvent)
            {
                var eventDefinitions = GetEventDefinitions(startEvent);
                if (eventDefinitions.Count > 0 && eventDefinitions[0] is TimerEventDefinition)
                {
                    if (container is SubProcess { TriggeredByEvent: false })
                    {
                        AddWarning(errors, "START_EVENT_TIMER_IN_SUBPROCESS",
                            "Timer start event in a non-event subprocess may not behave as expected", process, startEvent);
                    }
                }
            }

            if (flowElement is SubProcess nestedSubProcess)
            {
                ValidateTimerStartEventInContainer(nestedSubProcess, process, errors);
            }
        }
    }
}

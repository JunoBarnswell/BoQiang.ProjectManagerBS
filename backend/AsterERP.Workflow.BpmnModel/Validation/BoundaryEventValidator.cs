using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class BoundaryEventValidator : ProcessLevelValidator
{
    public override string ValidatorName => "BoundaryEventValidator";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        var boundaryEvents = FindFlowElementsOfType<BoundaryEvent>(process);

        var cancelBoundaryEventCounts = new Dictionary<string, int>();
        var compensateBoundaryEventCounts = new Dictionary<string, int>();

        foreach (var boundaryEvent in boundaryEvents)
        {
            ValidateAttachedToRef(process, boundaryEvent, errors);
            ValidateOutgoingFlow(process, boundaryEvent, errors);
            ValidateEventDefinition(process, boundaryEvent, cancelBoundaryEventCounts, compensateBoundaryEventCounts, errors);
        }

        ValidateMultipleCancelBoundaryEvents(process, cancelBoundaryEventCounts, errors);
        ValidateMultipleCompensateBoundaryEvents(process, compensateBoundaryEventCounts, errors);
    }

    private void ValidateAttachedToRef(Process process, BoundaryEvent boundaryEvent, List<ValidationError> errors)
    {
        if (string.IsNullOrEmpty(boundaryEvent.AttachedToRefId))
        {
            AddError(errors, "BOUNDARY_EVENT_NO_ATTACHED_TO_REF",
                "Boundary event must specify attachedToRef", process, boundaryEvent);
            return;
        }

        var attachedElement = FindFlowElementById(process, boundaryEvent.AttachedToRefId);
        if (attachedElement == null)
        {
            AddError(errors, "BOUNDARY_EVENT_INVALID_ATTACHED_TO_REF",
                $"Boundary event attachedToRef '{boundaryEvent.AttachedToRefId}' does not exist", process, boundaryEvent);
            return;
        }

        if (attachedElement is not Activity)
        {
            AddError(errors, "BOUNDARY_EVENT_ATTACHED_TO_NON_ACTIVITY",
                "Boundary event must be attached to an activity", process, boundaryEvent);
        }
    }

    private void ValidateOutgoingFlow(Process process, BoundaryEvent boundaryEvent, List<ValidationError> errors)
    {
        if (boundaryEvent.Id != null && GetOutgoingFlows(process, boundaryEvent.Id).Count == 0)
        {
            AddError(errors, "BOUNDARY_EVENT_NO_OUTGOING_FLOW",
                "Boundary event must have at least one outgoing sequence flow", process, boundaryEvent);
        }
    }

    private void ValidateEventDefinition(
        Process process,
        BoundaryEvent boundaryEvent,
        Dictionary<string, int> cancelBoundaryEventCounts,
        Dictionary<string, int> compensateBoundaryEventCounts,
        List<ValidationError> errors)
    {
        var eventDefinitions = GetEventDefinitions(boundaryEvent);
        if (eventDefinitions.Count == 0)
        {
            AddError(errors, "BOUNDARY_EVENT_NO_EVENT_DEFINITION",
                "Boundary event must have an event definition", process, boundaryEvent);
            return;
        }

        var eventDefinition = eventDefinitions[0];

        if (eventDefinition is not TimerEventDefinition &&
            eventDefinition is not ErrorEventDefinition &&
            eventDefinition is not SignalEventDefinition &&
            eventDefinition is not CancelEventDefinition &&
            eventDefinition is not MessageEventDefinition &&
            eventDefinition is not CompensateEventDefinition &&
            eventDefinition is not EscalationEventDefinition &&
            eventDefinition is not ConditionalEventDefinition)
        {
            AddError(errors, "BOUNDARY_EVENT_INVALID_EVENT_DEFINITION",
                "Boundary event only supports timer, error, signal, cancel, message, compensate, escalation, or conditional event definitions", process, boundaryEvent);
        }

        if (eventDefinition is CancelEventDefinition)
        {
            var attachedElement = string.IsNullOrEmpty(boundaryEvent.AttachedToRefId)
                ? null
                : FindFlowElementById(process, boundaryEvent.AttachedToRefId);

            if (attachedElement is not Transaction)
            {
                AddError(errors, "BOUNDARY_EVENT_CANCEL_ONLY_ON_TRANSACTION",
                    "Cancel boundary event can only be attached to a transaction", process, boundaryEvent);
            }
            else
            {
                var key = attachedElement.Id ?? "";
                cancelBoundaryEventCounts[key] = cancelBoundaryEventCounts.GetValueOrDefault(key) + 1;
            }
        }

        if (eventDefinition is CompensateEventDefinition)
        {
            var key = boundaryEvent.AttachedToRefId ?? "";
            compensateBoundaryEventCounts[key] = compensateBoundaryEventCounts.GetValueOrDefault(key) + 1;
        }
    }

    private void ValidateMultipleCancelBoundaryEvents(Process process, Dictionary<string, int> cancelBoundaryEventCounts, List<ValidationError> errors)
    {
        foreach (var kvp in cancelBoundaryEventCounts)
        {
            if (kvp.Value > 1)
            {
                var element = FindFlowElementById(process, kvp.Key);
                AddError(errors, "BOUNDARY_EVENT_MULTIPLE_CANCEL_ON_TRANSACTION",
                    "Multiple cancel boundary events attached to the same transaction", process, element as FlowElement);
            }
        }
    }

    private void ValidateMultipleCompensateBoundaryEvents(Process process, Dictionary<string, int> compensateBoundaryEventCounts, List<ValidationError> errors)
    {
        foreach (var kvp in compensateBoundaryEventCounts)
        {
            if (kvp.Value > 1)
            {
                var element = FindFlowElementById(process, kvp.Key);
                AddError(errors, "COMPENSATE_EVENT_MULTIPLE_ON_BOUNDARY",
                    "Multiple compensate boundary events attached to the same activity", process, element as FlowElement);
            }
        }
    }
}

using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class IntermediateCatchEventValidator : ProcessLevelValidator
{
    public override string ValidatorName => "IntermediateCatchEventValidator";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        var intermediateCatchEvents = FindFlowElementsOfType<IntermediateCatchEvent>(process);

        foreach (var intermediateCatchEvent in intermediateCatchEvents)
        {
            ValidateEventDefinition(process, intermediateCatchEvent, errors);
            ValidateFlows(process, intermediateCatchEvent, errors);
        }
    }

    private void ValidateEventDefinition(Process process, IntermediateCatchEvent intermediateCatchEvent, List<ValidationError> errors)
    {
        var eventDefinitions = GetEventDefinitions(intermediateCatchEvent);
        EventDefinition? eventDefinition = null;
        if (eventDefinitions.Count > 0)
        {
            eventDefinition = eventDefinitions[0];
        }

        if (eventDefinition == null)
        {
            AddError(errors, "INTERMEDIATE_CATCH_EVENT_NO_EVENTDEFINITION",
                "Intermediate catch event must have an event definition", process, intermediateCatchEvent);
            return;
        }

        if (eventDefinition is not TimerEventDefinition &&
            eventDefinition is not SignalEventDefinition &&
            eventDefinition is not MessageEventDefinition &&
            eventDefinition is not LinkEventDefinition &&
            eventDefinition is not ConditionalEventDefinition)
        {
            AddError(errors, "INTERMEDIATE_CATCH_EVENT_INVALID_EVENTDEFINITION",
                "Intermediate catch event has an unsupported event definition type. Only timer, signal, message, link, and conditional are supported", process, intermediateCatchEvent);
        }
    }

    private void ValidateFlows(Process process, IntermediateCatchEvent intermediateCatchEvent, List<ValidationError> errors)
    {
        var incomingFlows = GetIncomingFlows(process, intermediateCatchEvent.Id!);
        if (incomingFlows.Count == 0)
        {
            AddError(errors, "INTERMEDIATE_CATCH_EVENT_NO_INCOMING_FLOW",
                "Intermediate catch event must have at least one incoming sequence flow", process, intermediateCatchEvent);
        }

        var outgoingFlows = GetOutgoingFlows(process, intermediateCatchEvent.Id!);
        if (outgoingFlows.Count == 0)
        {
            AddError(errors, "INTERMEDIATE_CATCH_EVENT_NO_OUTGOING_FLOW",
                "Intermediate catch event must have at least one outgoing sequence flow", process, intermediateCatchEvent);
        }
    }
}

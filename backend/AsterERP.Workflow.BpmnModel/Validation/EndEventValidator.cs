using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class EndEventValidator : ProcessLevelValidator
{
    public override string ValidatorName => "EndEventValidator";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        var endEvents = FindDirectFlowElementsOfType<EndEvent>(process);

        if (endEvents.Count == 0)
        {
            AddError(errors, "END_EVENT_MISSING",
                "Process must have at least one end event", process);
        }

        foreach (var endEvent in endEvents)
        {
            if (endEvent.Id != null && GetOutgoingFlows(process, endEvent.Id).Count > 0)
            {
                AddError(errors, "END_EVENT_HAS_OUTGOING_FLOW",
                    "End event must not have outgoing sequence flow", process, endEvent);
            }

            if (endEvent.Id != null && GetIncomingFlows(process, endEvent.Id).Count == 0)
            {
                AddError(errors, "END_EVENT_NO_INCOMING_FLOW",
                    "End event must have at least one incoming sequence flow", process, endEvent);
            }
        }

        ValidateEndEventDefinitions(endEvents, process, errors);
    }

    private void ValidateEndEventDefinitions(List<EndEvent> endEvents, Process process, List<ValidationError> errors)
    {
        foreach (var endEvent in endEvents)
        {
            var eventDefinitions = GetEventDefinitions(endEvent);
            if (eventDefinitions.Count > 0)
            {
                var eventDef = eventDefinitions[0];
                if (eventDef is CancelEventDefinition)
                {
                    AddError(errors, "END_EVENT_CANCEL_ONLY_INSIDE_TRANSACTION",
                        "Cancel end event is only allowed inside a transaction subprocess", process, endEvent);
                }
            }
        }
    }
}

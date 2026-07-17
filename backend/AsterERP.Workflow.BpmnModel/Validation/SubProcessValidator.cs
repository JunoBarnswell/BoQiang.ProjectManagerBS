using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class SubProcessValidator : ProcessLevelValidator
{
    public override string ValidatorName => "SubProcessValidator";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        var subProcesses = FindFlowElementsOfType<SubProcess>(process);

        foreach (var subProcess in subProcesses)
        {
            ValidateSubProcess(process, subProcess, errors);
        }
    }

    private void ValidateSubProcess(Process process, SubProcess subProcess, List<ValidationError> errors)
    {
        if (subProcess is EventSubProcess)
            return;

        var startEvents = FindDirectFlowElementsOfType<StartEvent>(subProcess);
        var endEvents = FindDirectFlowElementsOfType<EndEvent>(subProcess);

        if (startEvents.Count == 0)
        {
            AddError(errors, "SUBPROCESS_MISSING_START_EVENT",
                "Subprocess must have at least one start event", process, subProcess);
        }

        if (startEvents.Count > 1)
        {
            AddError(errors, "SUBPROCESS_MULTIPLE_START_EVENTS",
                "Subprocess must not have multiple start events", process, subProcess);
        }

        foreach (var startEvent in startEvents)
        {
            var eventDefinitions = GetEventDefinitions(startEvent);
            if (eventDefinitions.Count > 0)
            {
                AddError(errors, "SUBPROCESS_START_EVENT_EVENT_DEFINITION_NOT_ALLOWED",
                    "Start event in a non-event subprocess must not have an event definition", process, startEvent);
            }
        }

        if (endEvents.Count == 0)
        {
            AddError(errors, "SUBPROCESS_MISSING_END_EVENT",
                "Subprocess must have at least one end event", process, subProcess);
        }

        ValidateNestingDepth(process, subProcess, 1, errors);
    }

    private void ValidateNestingDepth(Process process, SubProcess subProcess, int depth, List<ValidationError> errors)
    {
        if (depth > 3)
        {
            AddWarning(errors, "SUBPROCESS_DEEP_NESTING",
                $"Subprocess nesting depth is {depth}. Deep nesting may make the process hard to maintain", process, subProcess);
        }

        foreach (var flowElement in subProcess.FlowElements)
        {
            if (flowElement is SubProcess nestedSubProcess)
            {
                ValidateNestingDepth(process, nestedSubProcess, depth + 1, errors);
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class ProcessValidatorRules : ProcessLevelValidator
{
    public override string ValidatorName => "ProcessValidatorRules";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        ValidateProcessId(process, errors);
        ValidateExecutable(model, process, errors);
        ValidateOrphanedElements(process, errors);
        ValidateUnreachableElements(process, errors);
    }

    private void ValidateProcessId(Process process, List<ValidationError> errors)
    {
        if (string.IsNullOrEmpty(process.Id))
        {
            AddError(errors, "PROCESS_DEFINITION_MISSING_ID",
                "Process must have an id", process);
            return;
        }

        if (process.Id.Length > 0 && char.IsDigit(process.Id[0]))
        {
            AddError(errors, "PROCESS_DEFINITION_ID_STARTS_WITH_NUMBER",
                "Process id must not start with a number", process);
        }
    }

    private void ValidateExecutable(BpmnModel model, Process process, List<ValidationError> errors)
    {
        if (!process.IsExecutable)
        {
            var hasExecutable = model.Processes.Any(p => p.IsExecutable);
            if (!hasExecutable)
            {
                AddError(errors, "ALL_PROCESS_DEFINITIONS_NOT_EXECUTABLE",
                    "At least one process definition must be executable", process);
            }
            else
            {
                AddWarning(errors, "PROCESS_DEFINITION_NOT_EXECUTABLE",
                    "Process definition is not executable", process);
            }
        }
    }

    private void ValidateOrphanedElements(Process process, List<ValidationError> errors)
    {
        var connectedElementIds = new HashSet<string>();
        var allFlowElements = new List<FlowElement>();

        CollectAllFlowElements(process, allFlowElements);

        foreach (var flowElement in allFlowElements)
        {
            if (flowElement is SequenceFlow sequenceFlow)
            {
                if (!string.IsNullOrEmpty(sequenceFlow.SourceRef))
                    connectedElementIds.Add(sequenceFlow.SourceRef);
                if (!string.IsNullOrEmpty(sequenceFlow.TargetRef))
                    connectedElementIds.Add(sequenceFlow.TargetRef);
            }
        }

        foreach (var flowElement in allFlowElements)
        {
            if (flowElement is SequenceFlow or BoundaryEvent)
                continue;

            if (!string.IsNullOrEmpty(flowElement.Id) && !connectedElementIds.Contains(flowElement.Id))
            {
                if (flowElement is not StartEvent)
                {
                    AddWarning(errors, "ORPHANED_FLOW_ELEMENT",
                        $"Flow element '{flowElement.Id}' is not connected to any sequence flow", process, flowElement);
                }
            }
        }
    }

    private void ValidateUnreachableElements(Process process, List<ValidationError> errors)
    {
        var reachableIds = new HashSet<string>();
        var allFlowElements = new List<FlowElement>();
        CollectAllFlowElements(process, allFlowElements);

        var startEvents = allFlowElements.OfType<StartEvent>().ToList();
        var sequenceFlows = allFlowElements.OfType<SequenceFlow>().ToList();

        var queue = new Queue<string>();
        foreach (var startEvent in startEvents)
        {
            if (!string.IsNullOrEmpty(startEvent.Id))
            {
                reachableIds.Add(startEvent.Id);
                queue.Enqueue(startEvent.Id);
            }
        }

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            foreach (var outgoing in sequenceFlows.Where(sf => sf.SourceRef == currentId))
            {
                if (!string.IsNullOrEmpty(outgoing.TargetRef) && reachableIds.Add(outgoing.TargetRef))
                {
                    queue.Enqueue(outgoing.TargetRef);
                }
            }
        }

        foreach (var flowElement in allFlowElements)
        {
            if (flowElement is SequenceFlow or BoundaryEvent or StartEvent)
                continue;

            if (!string.IsNullOrEmpty(flowElement.Id) && !reachableIds.Contains(flowElement.Id))
            {
                AddWarning(errors, "UNREACHABLE_FLOW_ELEMENT",
                    $"Flow element '{flowElement.Id}' is unreachable from any start event", process, flowElement);
            }
        }
    }

    private void CollectAllFlowElements(IFlowElementsContainer container, List<FlowElement> result)
    {
        foreach (var element in container.FlowElements)
        {
            result.Add(element);
            if (element is IFlowElementsContainer nestedContainer)
                CollectAllFlowElements(nestedContainer, result);
        }
    }
}

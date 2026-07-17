using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class FlowElementValidator : ProcessLevelValidator
{
    private const int IdMaxLength = 255;

    public override string ValidatorName => "FlowElementValidator";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        ValidateFlowElements(process, process, errors);
    }

    private void ValidateFlowElements(Process process, IFlowElementsContainer container, List<ValidationError> errors)
    {
        var idSet = new HashSet<string>();

        foreach (var flowElement in container.FlowElements)
        {
            if (string.IsNullOrEmpty(flowElement.Id))
            {
                AddError(errors, "FLOW_ELEMENT_MISSING_ID",
                    "Flow element must have an id", process, flowElement);
                continue;
            }

            if (flowElement.Id.Contains(' '))
            {
                AddError(errors, "FLOW_ELEMENT_ID_CONTAINS_SPACE",
                    "Flow element id must not contain spaces", process, flowElement);
            }

            if (flowElement.Id.Length > IdMaxLength)
            {
                AddError(errors, "FLOW_ELEMENT_ID_TOO_LONG",
                    $"Flow element id must not exceed {IdMaxLength} characters", process, flowElement);
            }

            if (!idSet.Add(flowElement.Id))
            {
                AddError(errors, "FLOW_ELEMENT_ID_NOT_UNIQUE",
                    $"Duplicate flow element id '{flowElement.Id}' found in process", process, flowElement);
            }

            if (flowElement is SequenceFlow sequenceFlow)
            {
                ValidateSequenceFlowReferences(process, sequenceFlow, container, errors);
            }

            if (flowElement is Activity activity)
            {
                ValidateActivityConstraints(process, activity, errors);
            }

            if (flowElement is SubProcess subProcess)
            {
                ValidateFlowElements(process, subProcess, errors);
            }
        }
    }

    private void ValidateSequenceFlowReferences(Process process, SequenceFlow sequenceFlow, IFlowElementsContainer container, List<ValidationError> errors)
    {
        if (string.IsNullOrEmpty(sequenceFlow.SourceRef))
        {
            AddError(errors, "SEQ_FLOW_INVALID_SRC",
                "Sequence flow must have a sourceRef", process, sequenceFlow);
        }
        else
        {
            var sourceElement = FindFlowElementById(process, sequenceFlow.SourceRef);
            if (sourceElement == null)
            {
                AddError(errors, "SEQ_FLOW_INVALID_SRC",
                    $"Sequence flow sourceRef '{sequenceFlow.SourceRef}' does not exist", process, sequenceFlow);
            }
        }

        if (string.IsNullOrEmpty(sequenceFlow.TargetRef))
        {
            AddError(errors, "SEQ_FLOW_INVALID_TARGET",
                "Sequence flow must have a targetRef", process, sequenceFlow);
        }
        else
        {
            var targetElement = FindFlowElementById(process, sequenceFlow.TargetRef);
            if (targetElement == null)
            {
                AddError(errors, "SEQ_FLOW_INVALID_TARGET",
                    $"Sequence flow targetRef '{sequenceFlow.TargetRef}' does not exist", process, sequenceFlow);
            }
        }

        if (!string.IsNullOrEmpty(sequenceFlow.SourceRef) &&
            !string.IsNullOrEmpty(sequenceFlow.TargetRef) &&
            sequenceFlow.SourceRef == sequenceFlow.TargetRef)
        {
            AddError(errors, "SEQ_FLOW_SELF_REFERENCE",
                "Sequence flow sourceRef and targetRef must not be the same", process, sequenceFlow);
        }
    }

    private void ValidateActivityConstraints(Process process, Activity activity, List<ValidationError> errors)
    {
        if (activity.LoopCharacteristics != null)
        {
            var loop = activity.LoopCharacteristics;
            if (string.IsNullOrEmpty(loop.LoopCardinality) &&
                string.IsNullOrEmpty(loop.InputDataItem) &&
                string.IsNullOrEmpty(loop.CollectionString))
            {
                AddError(errors, "MULTI_INSTANCE_MISSING_COLLECTION",
                    "Multi-instance activity must have loopCardinality, inputDataItem, or collection defined", process, activity);
            }
        }
    }
}

using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class SequenceFlowValidator : ProcessLevelValidator
{
    public override string ValidatorName => "SequenceFlowValidator";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        var sequenceFlows = FindFlowElementsOfType<SequenceFlow>(process);

        foreach (var sequenceFlow in sequenceFlows)
        {
            ValidateSourceAndTarget(process, sequenceFlow, errors);
            ValidateScopeCrossing(process, sequenceFlow, errors);
            ValidateConditionalExpression(process, sequenceFlow, errors);
        }
    }

    private void ValidateSourceAndTarget(Process process, SequenceFlow sequenceFlow, List<ValidationError> errors)
    {
        if (string.IsNullOrEmpty(sequenceFlow.SourceRef))
        {
            AddError(errors, "SEQ_FLOW_INVALID_SRC",
                "Sequence flow must have a sourceRef", process, sequenceFlow);
        }
        else
        {
            var source = FindFlowElementById(process, sequenceFlow.SourceRef);
            if (source == null)
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
            var target = FindFlowElementById(process, sequenceFlow.TargetRef);
            if (target == null)
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

    private void ValidateScopeCrossing(Process process, SequenceFlow sequenceFlow, List<ValidationError> errors)
    {
        if (string.IsNullOrEmpty(sequenceFlow.SourceRef) || string.IsNullOrEmpty(sequenceFlow.TargetRef))
            return;

        var source = FindFlowElementById(process, sequenceFlow.SourceRef);
        var target = FindFlowElementById(process, sequenceFlow.TargetRef);

        if (source == null || target == null)
            return;

        var sourceContainer = FindContainerForElement(process, source.Id!);
        var targetContainer = FindContainerForElement(process, target.Id!);

        if (sourceContainer != null && targetContainer != null && sourceContainer != targetContainer)
        {
            AddError(errors, "SEQ_FLOW_INVALID_TARGET_DIFFERENT_SCOPE",
                "Sequence flow cannot cross (sub)process boundaries", process, sequenceFlow);
        }
    }

    private void ValidateConditionalExpression(Process process, SequenceFlow sequenceFlow, List<ValidationError> errors)
    {
        if (string.IsNullOrEmpty(sequenceFlow.ConditionExpression))
            return;

        var source = string.IsNullOrEmpty(sequenceFlow.SourceRef)
            ? null
            : FindFlowElementById(process, sequenceFlow.SourceRef);

        if (source is ExclusiveGateway or InclusiveGateway)
            return;

        if (source is not (ExclusiveGateway or InclusiveGateway) && source is Gateway)
        {
            AddWarning(errors, "SEQ_FLOW_CONDITIONAL_EXPRESSION_NOT_EFFECTIVE",
                "Conditional expression on sequence flow is only effective after exclusive or inclusive gateways", process, sequenceFlow);
        }
    }
}

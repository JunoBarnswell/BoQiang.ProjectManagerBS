using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel.Validation;

public class GatewayValidator : ProcessLevelValidator
{
    public override string ValidatorName => "GatewayValidator";

    protected override void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors)
    {
        var gateways = FindFlowElementsOfType<Gateway>(process);

        foreach (var gateway in gateways)
        {
            ValidateGatewayFlows(process, gateway, errors);
        }

        ValidateExclusiveGateways(process, errors);
        ValidateParallelGateways(process, errors);
    }

    private void ValidateGatewayFlows(Process process, Gateway gateway, List<ValidationError> errors)
    {
        if (gateway.Id == null)
            return;

        if (GetIncomingFlows(process, gateway.Id).Count == 0)
        {
            AddError(errors, "GATEWAY_NO_INCOMING_FLOW",
                "Gateway must have at least one incoming sequence flow", process, gateway);
        }

        if (GetOutgoingFlows(process, gateway.Id).Count == 0)
        {
            AddError(errors, "GATEWAY_NO_OUTGOING_FLOW",
                "Gateway must have at least one outgoing sequence flow", process, gateway);
        }
    }

    private void ValidateExclusiveGateways(Process process, List<ValidationError> errors)
    {
        var exclusiveGateways = FindFlowElementsOfType<ExclusiveGateway>(process);

        foreach (var gateway in exclusiveGateways)
        {
            var outgoingFlows = GetOutgoingFlows(process, gateway.Id!);

            if (outgoingFlows.Count == 0)
            {
                AddError(errors, "EXCLUSIVE_GATEWAY_NO_OUTGOING_SEQ_FLOW",
                    "Exclusive gateway must have at least one outgoing sequence flow", process, gateway);
            }
            else if (outgoingFlows.Count == 1)
            {
                var sequenceFlow = outgoingFlows[0];
                if (!string.IsNullOrEmpty(sequenceFlow.ConditionExpression))
                {
                    AddError(errors, "EXCLUSIVE_GATEWAY_CONDITION_NOT_ALLOWED_ON_SINGLE_SEQ_FLOW",
                        "Exclusive gateway with single outgoing sequence flow must not have a condition", process, gateway);
                }
            }
            else
            {
                ValidateExclusiveGatewayMultipleOutgoing(process, gateway, outgoingFlows, errors);
            }
        }
    }

    private void ValidateExclusiveGatewayMultipleOutgoing(Process process, ExclusiveGateway gateway, List<SequenceFlow> outgoingFlows, List<ValidationError> errors)
    {
        var defaultFlow = gateway.DefaultFlow;
        var flowsWithoutCondition = new List<SequenceFlow>();

        foreach (var flow in outgoingFlows)
        {
            var isDefaultFlow = flow.Id != null && flow.Id == defaultFlow;
            var hasCondition = !string.IsNullOrEmpty(flow.ConditionExpression);

            if (!hasCondition && !isDefaultFlow)
                flowsWithoutCondition.Add(flow);

            if (hasCondition && isDefaultFlow)
            {
                AddError(errors, "EXCLUSIVE_GATEWAY_CONDITION_ON_DEFAULT_SEQ_FLOW",
                    "Exclusive gateway default flow must not have a condition expression", process, gateway);
            }
        }

        if (flowsWithoutCondition.Count > 0)
        {
            AddWarning(errors, "EXCLUSIVE_GATEWAY_SEQ_FLOW_WITHOUT_CONDITIONS",
                "Exclusive gateway has outgoing sequence flows without conditions", process, gateway);
        }
    }

    private void ValidateParallelGateways(Process process, List<ValidationError> errors)
    {
        var parallelGateways = FindFlowElementsOfType<ParallelGateway>(process);

        foreach (var gateway in parallelGateways)
        {
            var incomingFlows = GetIncomingFlows(process, gateway.Id!);
            var outgoingFlows = GetOutgoingFlows(process, gateway.Id!);

            if (incomingFlows.Count > 1 && outgoingFlows.Count > 1)
            {
                AddWarning(errors, "PARALLEL_GATEWAY_MIXED",
                    "Parallel gateway with both multiple incoming and outgoing flows can be confusing; consider splitting into fork and join", process, gateway);
            }
        }
    }
}

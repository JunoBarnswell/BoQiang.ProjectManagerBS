using System;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public static class BpmnBehaviorBinder
{
    public static void BindBehaviors(BpmnModelNs.BpmnModel model, IActivityBehaviorFactory behaviorFactory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(behaviorFactory);

        foreach (var process in model.Processes)
        {
            BindBehaviors(process, behaviorFactory);
        }
    }

    public static void BindBehaviors(BpmnModelNs.Process process, IActivityBehaviorFactory behaviorFactory)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(behaviorFactory);

        foreach (var flowElement in process.FlowElements)
        {
            BindFlowElement(flowElement, behaviorFactory);
        }
    }

    private static void BindFlowElement(BpmnModelNs.FlowElement flowElement, IActivityBehaviorFactory behaviorFactory)
    {
        if (flowElement is BpmnModelNs.FlowNode flowNode)
        {
            flowNode.Behavior = CreateBehavior(flowNode, behaviorFactory);
        }

        if (flowElement is BpmnModelNs.SubProcess subProcess)
        {
            foreach (var child in subProcess.FlowElements)
            {
                BindFlowElement(child, behaviorFactory);
            }
        }
    }

    private static IBpmnActivityBehavior? CreateBehavior(BpmnModelNs.FlowNode flowNode, IActivityBehaviorFactory behaviorFactory)
    {
        var behavior = flowNode switch
        {
            BpmnModelNs.StartEvent startEvent => behaviorFactory.CreateStartEventBehavior(startEvent),
            BpmnModelNs.EndEvent endEvent => behaviorFactory.CreateEndEventBehavior(endEvent),
            BpmnModelNs.UserTask userTask => behaviorFactory.CreateUserTaskBehavior(userTask),
            BpmnModelNs.ServiceTask serviceTask => behaviorFactory.CreateServiceTaskBehavior(serviceTask),
            BpmnModelNs.ScriptTask scriptTask => behaviorFactory.CreateScriptTaskBehavior(scriptTask),
            BpmnModelNs.ReceiveTask receiveTask => behaviorFactory.CreateReceiveTaskBehavior(receiveTask),
            BpmnModelNs.SendTask sendTask => new SendTaskActivityBehavior(sendTask),
            BpmnModelNs.ManualTask manualTask => behaviorFactory.CreateManualTaskBehavior(manualTask),
            BpmnModelNs.BusinessRuleTask businessRuleTask => behaviorFactory.CreateBusinessRuleTaskBehavior(businessRuleTask),
            BpmnModelNs.EventGateway eventGateway => new EventGatewayActivityBehavior(),
            BpmnModelNs.ExclusiveGateway exclusiveGateway => behaviorFactory.CreateExclusiveGatewayBehavior(exclusiveGateway),
            BpmnModelNs.ParallelGateway parallelGateway => behaviorFactory.CreateParallelGatewayBehavior(parallelGateway),
            BpmnModelNs.InclusiveGateway inclusiveGateway => behaviorFactory.CreateInclusiveGatewayBehavior(inclusiveGateway),
            BpmnModelNs.ComplexGateway complexGateway => new ComplexGatewayActivityBehavior(),
            BpmnModelNs.CallActivity callActivity => behaviorFactory.CreateCallActivityBehavior(callActivity),
            BpmnModelNs.SubProcess subProcess => behaviorFactory.CreateSubProcessBehavior(subProcess),
            BpmnModelNs.BoundaryEvent boundaryEvent => behaviorFactory.CreateBoundaryEventBehavior(boundaryEvent),
            BpmnModelNs.IntermediateCatchEvent catchEvent => behaviorFactory.CreateIntermediateCatchEventBehavior(catchEvent),
            BpmnModelNs.IntermediateThrowEvent throwEvent => behaviorFactory.CreateIntermediateThrowEventBehavior(throwEvent),
            _ => null
        };

        if (flowNode is BpmnModelNs.Activity activity &&
            activity.LoopCharacteristics != null &&
            behavior != null)
        {
            return behaviorFactory.CreateMultiInstanceActivityBehavior(activity, behavior);
        }

        return behavior;
    }
}

using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public interface IActivityBehaviorFactory
{
    IBpmnActivityBehavior CreateServiceTaskBehavior(BpmnModelNs.ServiceTask serviceTask);
    IBpmnActivityBehavior CreateUserTaskBehavior(BpmnModelNs.UserTask userTask);
    IBpmnActivityBehavior CreateExclusiveGatewayBehavior(BpmnModelNs.ExclusiveGateway gateway);
    IBpmnActivityBehavior CreateParallelGatewayBehavior(BpmnModelNs.ParallelGateway gateway);
    IBpmnActivityBehavior CreateInclusiveGatewayBehavior(BpmnModelNs.InclusiveGateway gateway);
    IBpmnActivityBehavior CreateSubProcessBehavior(BpmnModelNs.SubProcess subProcess);
    IBpmnActivityBehavior CreateCallActivityBehavior(BpmnModelNs.CallActivity callActivity);
    IBpmnActivityBehavior CreateScriptTaskBehavior(BpmnModelNs.ScriptTask scriptTask);
    IBpmnActivityBehavior CreateReceiveTaskBehavior(BpmnModelNs.ReceiveTask receiveTask);
    IBpmnActivityBehavior CreateManualTaskBehavior(BpmnModelNs.ManualTask manualTask);
    IBpmnActivityBehavior CreateBusinessRuleTaskBehavior(BpmnModelNs.BusinessRuleTask businessRuleTask);
    IBpmnActivityBehavior CreateStartEventBehavior(BpmnModelNs.StartEvent startEvent);
    IBpmnActivityBehavior CreateEndEventBehavior(BpmnModelNs.EndEvent endEvent);
    IBpmnActivityBehavior CreateBoundaryEventBehavior(BpmnModelNs.BoundaryEvent boundaryEvent);
    IBpmnActivityBehavior CreateIntermediateCatchEventBehavior(BpmnModelNs.IntermediateCatchEvent catchEvent);
    IBpmnActivityBehavior CreateIntermediateThrowEventBehavior(BpmnModelNs.IntermediateThrowEvent throwEvent);
    IBpmnActivityBehavior CreateMultiInstanceActivityBehavior(BpmnModelNs.Activity activity, IBpmnActivityBehavior innerBehavior);
    IBpmnActivityBehavior CreateMailTaskBehavior(BpmnModelNs.ServiceTask serviceTask);
    IBpmnActivityBehavior CreateWebServiceTaskBehavior(BpmnModelNs.ServiceTask serviceTask);
}

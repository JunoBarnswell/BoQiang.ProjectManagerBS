using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Helper;

public class ErrorThrowingEventListener : BaseDelegateEventListener
{
    public string? ErrorCode { get; set; }

    public override bool IsFailOnException => true;

    public override void OnEvent(IWorkflowEvent @event)
    {
        if (!IsValidEvent(@event))
            return;

        if (string.IsNullOrWhiteSpace(ErrorCode))
            throw new WorkflowEngineArgumentException("errorCode is null or empty");

        if (string.IsNullOrEmpty(@event.ExecutionId))
        {
            throw new WorkflowEngineException(
                "No execution context active and event is not related to an execution. No error event can be thrown.");
        }

        throw new BpmnError(ErrorCode);
    }
}

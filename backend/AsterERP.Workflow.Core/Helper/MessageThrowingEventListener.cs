using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Helper;

public class MessageThrowingEventListener : BaseDelegateEventListener
{
    public string? MessageName { get; set; }
    public List<MessageThrownWorkflowEvent> ThrownMessages { get; } = new();

    public override bool IsFailOnException => true;

    public override void OnEvent(IWorkflowEvent @event)
    {
        if (!IsValidEvent(@event))
            return;

        if (string.IsNullOrWhiteSpace(MessageName))
            throw new WorkflowEngineArgumentException("messageName is null or empty");

        if (string.IsNullOrEmpty(@event.ProcessInstanceId))
        {
            throw new WorkflowEngineArgumentException(
                "Cannot throw process-instance scoped message, since the dispatched event is not part of an ongoing process instance");
        }

        ThrownMessages.Add(new MessageThrownWorkflowEvent(
            MessageName,
            @event.ExecutionId,
            @event.ProcessInstanceId,
            @event.ProcessDefinitionId));
    }
}

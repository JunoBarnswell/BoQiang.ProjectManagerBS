namespace AsterERP.Workflow.Core.Event;

public interface IEventBus
{
    void Publish(IWorkflowEvent @event);
    void Subscribe(IWorkflowEventListener listener);
    void Subscribe(IWorkflowEventListener listener, WorkflowEventType eventType);
    void Unsubscribe(IWorkflowEventListener listener);
}

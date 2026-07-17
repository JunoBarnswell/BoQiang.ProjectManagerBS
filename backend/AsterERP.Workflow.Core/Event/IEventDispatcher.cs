namespace AsterERP.Workflow.Core.Event;

public interface IEventDispatcher
{
    bool IsEnabled { get; }
    void AddEventListener(IWorkflowEventListener listener);
    void AddEventListener(IWorkflowEventListener listener, params WorkflowEventType[] types);
    void RemoveEventListener(IWorkflowEventListener listener);
    void DispatchEvent(IWorkflowEvent @event);
    Task DispatchEventAsync(IWorkflowEvent @event, CancellationToken cancellationToken = default);
}

namespace AsterERP.Workflow.Core.Event;

public interface IWorkflowEventListener
{
    void OnEvent(IWorkflowEvent @event);

    /// <summary>
    /// Handles an event without synchronously blocking on asynchronous work.
    /// Existing synchronous listeners remain source-compatible through the default implementation.
    /// </summary>
    Task OnEventAsync(IWorkflowEvent @event, CancellationToken cancellationToken = default)
    {
        OnEvent(@event);
        return Task.CompletedTask;
    }

    bool IsFailOnException { get; }
}

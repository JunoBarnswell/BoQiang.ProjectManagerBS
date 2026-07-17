namespace AsterERP.Workflow.Core.Event;

public class EventDispatcherImplementation : IEventDispatcher
{
    private readonly List<IWorkflowEventListener> _listeners = new();
    private readonly Dictionary<WorkflowEventType, List<IWorkflowEventListener>> _typedListeners = new();

    public bool IsEnabled => _listeners.Count > 0 || _typedListeners.Count > 0;

    public void AddEventListener(IWorkflowEventListener listener)
    {
        if (!_listeners.Contains(listener))
        {
            _listeners.Add(listener);
        }
    }

    public void AddEventListener(IWorkflowEventListener listener, params WorkflowEventType[] types)
    {
        foreach (var type in types)
        {
            if (!_typedListeners.TryGetValue(type, out var list))
            {
                list = new List<IWorkflowEventListener>();
                _typedListeners[type] = list;
            }
            if (!list.Contains(listener))
            {
                list.Add(listener);
            }
        }
    }

    public void RemoveEventListener(IWorkflowEventListener listener)
    {
        _listeners.Remove(listener);
        foreach (var list in _typedListeners.Values)
        {
            list.Remove(listener);
        }
    }

    public void DispatchEvent(IWorkflowEvent @event)
    {
        DispatchToListeners(_listeners, @event);

        if (_typedListeners.TryGetValue(@event.Type, out var typedList))
        {
            DispatchToListeners(typedList, @event);
        }
    }

    public async Task DispatchEventAsync(
        IWorkflowEvent @event,
        CancellationToken cancellationToken = default)
    {
        await DispatchToListenersAsync(_listeners, @event, cancellationToken);

        if (_typedListeners.TryGetValue(@event.Type, out var typedList))
        {
            await DispatchToListenersAsync(typedList, @event, cancellationToken);
        }
    }

    private static void DispatchToListeners(
        IEnumerable<IWorkflowEventListener> listeners,
        IWorkflowEvent @event)
    {
        foreach (var listener in listeners)
        {
            try
            {
                listener.OnEvent(@event);
            }
            catch when (!listener.IsFailOnException)
            {
            }
        }
    }

    private static async Task DispatchToListenersAsync(
        IEnumerable<IWorkflowEventListener> listeners,
        IWorkflowEvent @event,
        CancellationToken cancellationToken)
    {
        foreach (var listener in listeners)
        {
            try
            {
                await listener.OnEventAsync(@event, cancellationToken);
            }
            catch when (!listener.IsFailOnException)
            {
            }
        }
    }
}

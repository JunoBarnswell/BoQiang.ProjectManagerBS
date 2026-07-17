using System.Threading.Channels;

namespace AsterERP.Workflow.Core.Event;

public class EventBusImplementation : IEventBus, IDisposable
{
    private readonly Channel<IWorkflowEvent> _channel;
    private readonly List<IWorkflowEventListener> _listeners = new();
    private readonly Dictionary<WorkflowEventType, List<IWorkflowEventListener>> _typedListeners = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lock = new();
    private bool _disposed;

    public EventBusImplementation()
    {
        _channel = Channel.CreateUnbounded<IWorkflowEvent>();
        _ = StartProcessing();
    }

    public void Publish(IWorkflowEvent @event)
    {
        DispatchToListeners(@event);
    }

    public void Subscribe(IWorkflowEventListener listener)
    {
        lock (_lock)
        {
            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }
    }

    public void Subscribe(IWorkflowEventListener listener, WorkflowEventType eventType)
    {
        lock (_lock)
        {
            if (!_typedListeners.TryGetValue(eventType, out var list))
            {
                list = new List<IWorkflowEventListener>();
                _typedListeners[eventType] = list;
            }
            if (!list.Contains(listener))
            {
                list.Add(listener);
            }
        }
    }

    public void Unsubscribe(IWorkflowEventListener listener)
    {
        lock (_lock)
        {
            _listeners.Remove(listener);
            foreach (var list in _typedListeners.Values)
            {
                list.Remove(listener);
            }
        }
    }

    private async global::System.Threading.Tasks.Task StartProcessing()
    {
        try
        {
            await foreach (var @event in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                DispatchToListeners(@event);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void DispatchToListeners(IWorkflowEvent @event)
    {
        List<IWorkflowEventListener> listenersCopy;
        List<IWorkflowEventListener>? typedCopy = null;

        lock (_lock)
        {
            listenersCopy = new List<IWorkflowEventListener>(_listeners);
            if (_typedListeners.TryGetValue(@event.Type, out var typed))
            {
                typedCopy = new List<IWorkflowEventListener>(typed);
            }
        }

        foreach (var listener in listenersCopy)
        {
            try
            {
                listener.OnEvent(@event);
            }
            catch when (!listener.IsFailOnException)
            {
            }
        }

        if (typedCopy != null)
        {
            foreach (var listener in typedCopy)
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
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _channel.Writer.TryComplete();
    }
}

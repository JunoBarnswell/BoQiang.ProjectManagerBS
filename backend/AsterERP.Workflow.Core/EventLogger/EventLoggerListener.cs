using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.EventLogger.EventHandler;
using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Core.EventLogger;

public class EventLoggerListener : IWorkflowEventListener
{
    private readonly Dictionary<WorkflowEventType, IEventHandler> _handlers;
    private readonly IEventLogger _eventLogger;
    private readonly HashSet<WorkflowEventType> _eventTypes;
    private readonly ILogger<EventLoggerListener>? _logger;

    public bool IsFailOnException => false;

    public EventLoggerListener(
        IEventLogger eventLogger,
        EventLoggerConfiguration configuration,
        ILogger<EventLoggerListener>? logger = null)
    {
        _eventLogger = eventLogger;
        _eventTypes = configuration.EventTypes;
        _logger = logger;
        _handlers = CreateHandlers();
    }

    public void OnEvent(IWorkflowEvent @event)
    {
        throw new NotSupportedException("EventLoggerListener is async-only. Use OnEventAsync.");
    }

    public async Task OnEventAsync(IWorkflowEvent @event, CancellationToken cancellationToken = default)
    {
        if (!_eventTypes.Contains(@event.Type)) return;

        if (!_handlers.TryGetValue(@event.Type, out var handler)) return;

        var entry = handler.HandleEvent(@event);
        if (entry != null)
        {
            try
            {
                await _eventLogger.LogEventAsync(entry, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to log event of type {EventType}", @event.Type);
            }
        }
    }

    private static Dictionary<WorkflowEventType, IEventHandler> CreateHandlers()
    {
        return new Dictionary<WorkflowEventType, IEventHandler>
        {
            [WorkflowEventType.PROCESS_STARTED] = new ProcessInstanceStartEventHandler(),
            [WorkflowEventType.PROCESS_COMPLETED] = new ProcessInstanceEndEventHandler(),
            [WorkflowEventType.TASK_CREATED] = new TaskCreatedEventHandler(),
            [WorkflowEventType.TASK_COMPLETED] = new TaskCompletedEventHandler(),
            [WorkflowEventType.ACTIVITY_STARTED] = new ActivityStartedEventHandler(),
            [WorkflowEventType.ACTIVITY_COMPLETED] = new ActivityCompletedEventHandler(),
            [WorkflowEventType.VARIABLE_CREATED] = new VariableCreatedEventHandler(),
            [WorkflowEventType.VARIABLE_UPDATED] = new VariableUpdatedEventHandler()
        };
    }
}

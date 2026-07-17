using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.EventLogger;

public class EventLoggerConfiguration
{
    public bool Enabled { get; set; } = true;

    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 50;

    public HashSet<WorkflowEventType> EventTypes { get; set; } = new()
    {
        WorkflowEventType.PROCESS_STARTED,
        WorkflowEventType.PROCESS_COMPLETED,
        WorkflowEventType.TASK_CREATED,
        WorkflowEventType.TASK_COMPLETED,
        WorkflowEventType.ACTIVITY_STARTED,
        WorkflowEventType.ACTIVITY_COMPLETED,
        WorkflowEventType.VARIABLE_CREATED,
        WorkflowEventType.VARIABLE_UPDATED,
        WorkflowEventType.VARIABLE_DELETED
    };

    public Type FlusherType { get; set; } = typeof(ConsoleEventFlusher);

    public static EventLoggerConfiguration Default => new();

    public static EventLoggerConfiguration Disable => new()
    {
        Enabled = false
    };

    public static EventLoggerConfiguration DatabaseOnly => new()
    {
        FlusherType = typeof(DatabaseEventFlusher),
        EventTypes = new HashSet<WorkflowEventType>
        {
            WorkflowEventType.PROCESS_STARTED,
            WorkflowEventType.PROCESS_COMPLETED,
            WorkflowEventType.TASK_CREATED,
            WorkflowEventType.TASK_COMPLETED
        }
    };
}

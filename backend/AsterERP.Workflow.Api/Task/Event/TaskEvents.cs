namespace AsterERP.Workflow.Api.Task.Event;

public abstract class TaskEvent
{
    public System.DateTime Timestamp { get; init; } = System.DateTime.UtcNow;
}

public class TaskCreatedEvent : TaskEvent
{
    public string TaskId { get; init; } = null!;
    public string? TaskName { get; init; }
    public string? ProcessInstanceId { get; init; }
}

public class TaskAssignedEvent : TaskEvent
{
    public string TaskId { get; init; } = null!;
    public string? Assignee { get; init; }
    public string? PreviousAssignee { get; init; }
}

public class TaskCompletedEvent : TaskEvent
{
    public string TaskId { get; init; } = null!;
    public string? ProcessInstanceId { get; init; }
}

public class TaskClaimedEvent : TaskEvent
{
    public string TaskId { get; init; } = null!;
    public string? Assignee { get; init; }
}

public class TaskReleasedEvent : TaskEvent
{
    public string TaskId { get; init; } = null!;
}

public class TaskDeletedEvent : TaskEvent
{
    public string TaskId { get; init; } = null!;
    public string? Reason { get; init; }
}

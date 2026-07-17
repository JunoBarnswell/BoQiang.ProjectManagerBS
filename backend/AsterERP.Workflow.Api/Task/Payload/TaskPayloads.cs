namespace AsterERP.Workflow.Api.Task.Payload;

public class CreateTaskPayload
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Assignee { get; init; }
    public int? Priority { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public string? FormKey { get; init; }
    public System.DateTime? DueDate { get; init; }
    public string? Category { get; init; }
    public string? TenantId { get; init; }
}

public class ClaimTaskPayload
{
    public string TaskId { get; init; } = null!;
    public string? Assignee { get; init; }
}

public class ReleaseTaskPayload
{
    public string TaskId { get; init; } = null!;
}

public class CompleteTaskPayload
{
    public string TaskId { get; init; } = null!;
    public Dictionary<string, object?>? Variables { get; init; }
}

public class UpdateTaskPayload
{
    public string TaskId { get; init; } = null!;
    public string? Name { get; init; }
    public string? Description { get; init; }
    public int? Priority { get; init; }
    public string? Assignee { get; init; }
    public string? Owner { get; init; }
    public System.DateTime? DueDate { get; init; }
    public string? Category { get; init; }
}

public class DeleteTaskPayload
{
    public string TaskId { get; init; } = null!;
    public string? Reason { get; init; }
}

public enum TaskStatus
{
    Created,
    Assigned,
    Suspended,
    Completed,
    Cancelled,
    Deleted
}

public class TaskPayload
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public string? Description { get; init; }
    public int? Priority { get; init; }
    public string? Assignee { get; init; }
    public string? Owner { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public string? ExecutionId { get; init; }
    public string? TaskDefinitionKey { get; init; }
    public System.DateTime? CreatedDate { get; init; }
    public System.DateTime? ClaimedDate { get; init; }
    public System.DateTime? DueDate { get; init; }
    public System.DateTime? CompletedDate { get; init; }
    public string? Category { get; init; }
    public string? FormKey { get; init; }
    public string? TenantId { get; init; }
    public TaskStatus Status { get; init; }
    public string? BusinessKey { get; init; }
    public string? ParentTaskId { get; init; }
}

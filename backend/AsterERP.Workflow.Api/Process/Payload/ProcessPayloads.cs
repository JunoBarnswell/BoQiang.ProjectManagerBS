namespace AsterERP.Workflow.Api.Process.Payload;

public class DeployPayload
{
    public string? ResourceName { get; init; }
    public byte[]? ResourceContent { get; init; }
    public string? TenantId { get; init; }
}

public class StartPayload
{
    public string? ProcessDefinitionKey { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public string? BusinessKey { get; init; }
    public string? Name { get; init; }
    public Dictionary<string, object?>? Variables { get; init; }
}

public class SuspendPayload
{
    public string ProcessInstanceId { get; init; } = null!;
}

public class ResumePayload
{
    public string ProcessInstanceId { get; init; } = null!;
}

public class DeletePayload
{
    public string ProcessInstanceId { get; init; } = null!;
    public string? Reason { get; init; }
}

public class GetProcessDefinitionPayload
{
    public string? ProcessDefinitionId { get; init; }
    public string? ProcessDefinitionKey { get; init; }
}

public class GetProcessInstancePayload
{
    public string ProcessInstanceId { get; init; } = null!;
}

public enum ProcessInstanceStatus
{
    Created,
    Running,
    Suspended,
    Cancelled,
    Completed
}

public class ProcessDefinitionPayload
{
    public string Id { get; init; } = null!;
    public string? Key { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public int Version { get; init; }
    public string? ResourceName { get; init; }
    public string? DeploymentId { get; init; }
    public string? DiagramResourceName { get; init; }
    public bool HasStartFormKey { get; init; }
    public string? StartFormKey { get; init; }
    public string? Category { get; init; }
    public string? TenantId { get; init; }
}

public class ProcessInstancePayload
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public string? BusinessKey { get; init; }
    public string ProcessDefinitionId { get; init; } = null!;
    public string? ProcessDefinitionKey { get; init; }
    public string? ProcessDefinitionName { get; init; }
    public int ProcessDefinitionVersion { get; init; }
    public string? StartUserId { get; init; }
    public System.DateTime? StartTime { get; init; }
    public System.DateTime? CompletedTime { get; init; }
    public string? TenantId { get; init; }
    public ProcessInstanceStatus Status { get; init; }
    public Dictionary<string, object?>? Variables { get; init; }
}

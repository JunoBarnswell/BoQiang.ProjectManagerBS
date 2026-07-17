namespace AsterERP.Workflow.Api.Process.Event;

public abstract class ProcessEvent
{
    public System.DateTime Timestamp { get; init; } = System.DateTime.UtcNow;
}

public class ProcessStartedEvent : ProcessEvent
{
    public string ProcessInstanceId { get; init; } = null!;
    public string? ProcessDefinitionId { get; init; }
    public string? ProcessDefinitionKey { get; init; }
}

public class ProcessCompletedEvent : ProcessEvent
{
    public string ProcessInstanceId { get; init; } = null!;
    public string? ProcessDefinitionId { get; init; }
}

public class ProcessSuspendedEvent : ProcessEvent
{
    public string ProcessInstanceId { get; init; } = null!;
}

public class ProcessResumedEvent : ProcessEvent
{
    public string ProcessInstanceId { get; init; } = null!;
}

public class ProcessCancelledEvent : ProcessEvent
{
    public string ProcessInstanceId { get; init; } = null!;
    public string? Reason { get; init; }
}

public class ProcessDeployedEvent : ProcessEvent
{
    public string? DeploymentId { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public string? ProcessDefinitionKey { get; init; }
}

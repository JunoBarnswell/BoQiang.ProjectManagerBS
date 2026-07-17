namespace AsterERP.Workflow.Core.Job;

public interface IJob
{
    string? Id { get; }
    DateTime? DueDate { get; }
    string? ProcessInstanceId { get; }
    string? ExecutionId { get; }
    string? ProcessDefinitionId { get; }
    int Retries { get; }
    string? ExceptionMessage { get; }
    string? TenantId { get; }
    bool IsExclusive { get; }
    string? JobType { get; }
    string? HandlerType { get; }
    string? HandlerConfiguration { get; }
}

public abstract class AbstractJobEntity : IJob
{
    public const string JobTypeTimer = "timer";
    public const string JobTypeMessage = "message";
    public const string JobTypeAsyncContinuation = "async-continuation";
    public const string JobTypeDeadLetter = "deadletter";
    public const bool DefaultExclusive = true;
    public const int MaxExceptionMessageLength = 255;

    public string? Id { get; set; } = AbpTimeIdProvider.NewGuid("N");
    public DateTime? DueDate { get; set; }
    public string? ExecutionId { get; set; }
    public string? ProcessInstanceId { get; set; }
    public string? ProcessDefinitionId { get; set; }
    public bool IsExclusive { get; set; } = DefaultExclusive;
    public int Retries { get; set; } = 3;
    public int MaxIterations { get; set; }
    public string? Repeat { get; set; }
    public DateTime? EndDate { get; set; }
    public string? HandlerType { get; set; }
    public string? HandlerConfiguration { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? ExceptionStackId { get; set; }
    public string? TenantId { get; set; } = "";
    public string? JobType { get; set; }
    public JobState State { get; set; } = JobState.Created;
    public DateTime CreatedTime { get; set; } = AbpTimeIdProvider.UtcNow;
}

public class JobEntity : AbstractJobEntity
{
    public string? LockOwner { get; set; }
    public DateTime? LockExpirationTime { get; set; }
}

public class TimerJobEntity : AbstractJobEntity
{
    public string? LockOwner { get; set; }
    public DateTime? LockExpirationTime { get; set; }
}

public class DeadLetterJobEntity : AbstractJobEntity
{
    public string? OriginalJobId { get; set; }
    public string? OriginalJobType { get; set; }
}

public enum JobType
{
    Timer,
    AsyncContinuation,
    Message,
    DeadLetter,
    EventSubscription,
    Custom
}

public enum JobState
{
    Created,
    Acquired,
    Executing,
    Completed,
    Failed,
    DeadLetter
}


namespace AsterERP.Workflow.Core.EventLogger;

public class EventLogEntry
{
    public string Id { get; init; } = AbpTimeIdProvider.NewGuid();
    public string Type { get; init; } = null!;
    public string? ProcessDefinitionId { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ExecutionId { get; init; }
    public string? TaskId { get; init; }
    public DateTime TimeStamp { get; init; } = AbpTimeIdProvider.UtcNow;
    public Dictionary<string, object?>? Data { get; init; }
}



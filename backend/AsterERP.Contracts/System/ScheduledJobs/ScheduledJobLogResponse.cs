namespace AsterERP.Contracts.System.ScheduledJobs;

public sealed record ScheduledJobLogResponse(
    string Id,
    string? JobId,
    string TriggerType,
    string Result,
    DateTime StartTime,
    DateTime? EndTime,
    long DurationMs,
    string? ErrorMessage,
    string? OutputSummary,
    string TraceId);

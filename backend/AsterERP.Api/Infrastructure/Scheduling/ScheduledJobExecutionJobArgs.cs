namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed record ScheduledJobExecutionJobArgs(
    string JobId,
    string Trigger);

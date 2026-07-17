namespace AsterERP.Contracts.System.ScheduledJobs;

public sealed record ScheduledJobSummaryResponse(
    int Total,
    int Enabled,
    int Paused,
    int Success,
    int Failed);

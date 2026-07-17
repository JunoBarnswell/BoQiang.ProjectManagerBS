namespace AsterERP.Api.Domain.System.ScheduledJobs;

public sealed record ScheduleBuildResult(
    string CronExpression,
    string FriendlySchedule,
    DateTime? NextRunAt,
    string TimeZoneId);

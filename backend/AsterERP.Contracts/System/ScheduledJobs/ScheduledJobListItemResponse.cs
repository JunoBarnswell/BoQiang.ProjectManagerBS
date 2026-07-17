namespace AsterERP.Contracts.System.ScheduledJobs;

public sealed record ScheduledJobListItemResponse(
    string Id,
    string Name,
    string Code,
    string JobType,
    string? PresetJobCode,
    string Status,
    string FriendlySchedule,
    string? LastResult,
    DateTime? LastRunAt,
    DateTime? NextRunAt,
    string ScheduleSyncStatus,
    DateTime CreatedTime,
    string? Remark);

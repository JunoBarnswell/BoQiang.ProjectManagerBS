namespace AsterERP.Contracts.System.ScheduledJobs;

public sealed record ScheduledJobDetailResponse(
    string Id,
    string Name,
    string Code,
    string JobType,
    string? PresetJobCode,
    string Status,
    ScheduleConfigDto Schedule,
    string? Parameters,
    HttpCallbackConfigDto? HttpCallback,
    string FriendlySchedule,
    string? LastResult,
    DateTime? LastRunAt,
    DateTime? NextRunAt,
    string ScheduleSyncStatus,
    string? LastSyncError,
    string? LastErrorMessage,
    string? Remark);

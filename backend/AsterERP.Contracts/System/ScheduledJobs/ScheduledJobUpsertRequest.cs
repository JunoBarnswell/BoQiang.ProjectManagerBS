namespace AsterERP.Contracts.System.ScheduledJobs;

public sealed record ScheduledJobUpsertRequest(
    string Name,
    string Code,
    string JobType,
    string Status,
    string? PresetJobCode,
    ScheduleConfigDto Schedule,
    string? Parameters,
    HttpCallbackConfigDto? HttpCallback,
    string? Remark);

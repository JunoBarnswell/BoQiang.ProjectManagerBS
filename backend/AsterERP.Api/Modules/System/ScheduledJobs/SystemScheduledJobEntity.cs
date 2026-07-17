using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.ScheduledJobs;

[SugarTable("system_scheduled_jobs")]
public sealed class SystemScheduledJobEntity : EntityBase
{
    public string JobName { get; set; } = string.Empty;

    public string JobCode { get; set; } = string.Empty;

    public string JobType { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? PresetJobCode { get; set; }

    public string Status { get; set; } = "Enabled";

    public string ScheduleKind { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public int? IntervalValue { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? TimeOfDay { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? WeekDaysJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? MonthDaysJson { get; set; }

    public string TimeZoneId { get; set; } = "China Standard Time";

    public string ScheduleConfigJson { get; set; } = "{}";

    [SugarColumn(IsNullable = true)]
    public string? ParameterJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? HttpCallbackJson { get; set; }

    public string CronExpression { get; set; } = string.Empty;

    public string FriendlySchedule { get; set; } = string.Empty;

    public string ScheduleSyncStatus { get; set; } = "Pending";

    [SugarColumn(IsNullable = true)]
    public string? LastSyncError { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? LastResult { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastRunAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? NextRunAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? LastErrorMessage { get; set; }
}

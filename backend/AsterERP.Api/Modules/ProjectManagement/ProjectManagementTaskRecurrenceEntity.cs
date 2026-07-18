using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_task_recurrences")]
public sealed class ProjectManagementTaskRecurrenceEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string SourceTaskId { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int Interval { get; set; } = 1;
    public string DaysOfWeekJson { get; set; } = "[]";
    [SugarColumn(IsNullable = true)] public int? DayOfMonth { get; set; }
    [SugarColumn(IsNullable = true)] public string? CustomUnit { get; set; }
    public DateTime StartAtLocal { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? EndsAtLocal { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public int GenerationWindowDays { get; set; }
    public string TaskSnapshotJson { get; set; } = string.Empty;
    public string SeriesOwnerUserId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public long VersionNo { get; set; } = 1;
}

[SugarTable("pm_task_recurrence_occurrences")]
public sealed class ProjectManagementTaskRecurrenceOccurrenceEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string RecurrenceId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string RecurrenceKey { get; set; } = string.Empty;
    public DateTime ScheduledAtLocal { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string State { get; set; } = "Generated";
    public long VersionNo { get; set; } = 1;
}

using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_task_reminders")]
public sealed class ProjectManagementTaskReminderEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;
    public DateTime ReminderAtUtc { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? Note { get; set; }
    public string Status { get; set; } = "Pending";
    public string IdempotencyKey { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? HangfireJobId { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    [SugarColumn(IsNullable = true)] public DateTime? LastAttemptAt { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? TriggeredAt { get; set; }
    [SugarColumn(IsNullable = true)] public string? LastError { get; set; }
    public long VersionNo { get; set; } = 1;
}

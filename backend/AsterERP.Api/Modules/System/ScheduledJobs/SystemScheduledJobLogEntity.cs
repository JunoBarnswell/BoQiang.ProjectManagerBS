using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.ScheduledJobs;

[SugarTable("system_scheduled_job_logs")]
public sealed class SystemScheduledJobLogEntity : EntityBase
{
    public string ScheduledJobId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? HangfireJobId { get; set; }

    public string TriggerType { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? FinishedAt { get; set; }

    public long DurationMs { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? OutputSummary { get; set; }

    public string TraceId { get; set; } = string.Empty;
}

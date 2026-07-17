using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_integration_task_runs")]
public sealed class ApplicationIntegrationTaskRunEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string TaskId { get; set; } = string.Empty;

    public string TriggerType { get; set; } = "Manual";

    public string Result { get; set; } = "Pending";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? FinishedAt { get; set; }

    public long DurationMs { get; set; }

    public int ReadCount { get; set; }

    public int SuccessCount { get; set; }

    public int FailedCount { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(Length = 262144, IsNullable = true)]
    public string? OutputJson { get; set; }
}

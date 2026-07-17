using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_connection_check_runs")]
public sealed class ApplicationConnectionCheckRunEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string TaskId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? DataSourceId { get; set; }

    public string TemplateCode { get; set; } = string.Empty;

    public string Result { get; set; } = "Pending";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? FinishedAt { get; set; }

    public long DurationMs { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(Length = 262144, IsNullable = true)]
    public string? ResultJson { get; set; }
}

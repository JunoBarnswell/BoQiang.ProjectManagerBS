using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.QueryViews;

[SugarTable("system_query_view_export_tasks")]
public sealed class SystemQueryViewExportTaskEntity : EntityBase
{
    public string TaskNo { get; set; } = string.Empty;

    public string ViewCode { get; set; } = string.Empty;

    public string ExportName { get; set; } = string.Empty;

    public string Status { get; set; } = "waiting";

    [SugarColumn(IsNullable = true)]
    public string? FileUrl { get; set; }

    public long TotalCount { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? FinishedTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }
}

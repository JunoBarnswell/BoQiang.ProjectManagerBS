using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.QueryViews;

[SugarTable("system_query_view_runtimes")]
public sealed class SystemQueryViewRuntimeEntity : EntityBase
{
    public string ViewId { get; set; } = string.Empty;

    public string StableViewName { get; set; } = string.Empty;

    public string CurrentVersionViewName { get; set; } = string.Empty;

    public int CurrentVersionNo { get; set; }

    public DateTime LastCheckTime { get; set; } = DateTime.UtcNow;

    public string HealthStatus { get; set; } = "healthy";

    [SugarColumn(IsNullable = true)]
    public string? LastError { get; set; }

    public long RowCountSnapshot { get; set; }
}

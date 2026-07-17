using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.QueryViews;

[SugarTable("system_query_view_publish_logs")]
public sealed class SystemQueryViewPublishLogEntity : EntityBase
{
    public string ViewId { get; set; } = string.Empty;

    public int VersionNo { get; set; }

    public string StableViewName { get; set; } = string.Empty;

    public string VersionViewName { get; set; } = string.Empty;

    public string Action { get; set; } = "Publish";

    public string PublishStatus { get; set; } = "Success";

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    public string PublishedBy { get; set; } = string.Empty;

    public DateTime PublishedTime { get; set; } = DateTime.UtcNow;
}

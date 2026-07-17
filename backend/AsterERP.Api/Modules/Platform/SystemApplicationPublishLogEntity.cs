using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Platform;

[SugarTable("system_application_publish_logs")]
public sealed class SystemApplicationPublishLogEntity : EntityBase
{
    public string TaskId { get; set; } = string.Empty;

    public string Level { get; set; } = "Info";

    public string Stage { get; set; } = "Queued";

    [SugarColumn(Length = 2000)]
    public string Message { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;
}

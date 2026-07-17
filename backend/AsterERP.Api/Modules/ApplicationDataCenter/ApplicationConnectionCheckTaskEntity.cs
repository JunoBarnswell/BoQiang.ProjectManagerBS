using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_connection_check_tasks")]
public sealed class ApplicationConnectionCheckTaskEntity : ApplicationDataCenterObjectEntity
{
    [SugarColumn(IsNullable = true)]
    public string? DataSourceId { get; set; }

    public string TemplateCode { get; set; } = string.Empty;

    public int RetryCount { get; set; } = 1;
}

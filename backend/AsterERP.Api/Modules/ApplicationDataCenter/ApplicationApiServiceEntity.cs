using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_api_services")]
public sealed class ApplicationApiServiceEntity : ApplicationDataCenterObjectEntity
{
    public string HttpMethod { get; set; } = "GET";

    public string RoutePath { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? SourceObjectId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PermissionCode { get; set; }

    public bool RequiresAuthentication { get; set; } = true;
}

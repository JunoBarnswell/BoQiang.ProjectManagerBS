using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_dev_versions")]
public sealed class ApplicationDevelopmentVersionEntity : EntityBase
{
    public string AppCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? DefaultPageId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SourceDataSourceId { get; set; }

    public string Status { get; set; } = "Draft";

    public string TenantId { get; set; } = string.Empty;

    public string VersionCode { get; set; } = string.Empty;

    public string VersionName { get; set; } = string.Empty;
}

using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_dev_shared_resources")]
public sealed class ApplicationSharedResourceEntity : EntityBase
{
    public string AppCode { get; set; } = string.Empty;

    public string ContentJson { get; set; } = "{}";

    [SugarColumn(IsNullable = true)]
    public string? ContentText { get; set; }

    public string ResourceCode { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public string TenantId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? VersionId { get; set; }
}

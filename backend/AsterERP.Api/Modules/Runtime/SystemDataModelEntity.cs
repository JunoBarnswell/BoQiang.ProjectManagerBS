using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Runtime;

[SugarTable("system_data_models")]
public sealed class SystemDataModelEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string ProviderKey { get; set; } = string.Empty;

    public string KeyField { get; set; } = "id";

    [SugarColumn(IsNullable = true)]
    public string? PermissionCode { get; set; }

    public int VersionNo { get; set; } = 1;

    public string Status { get; set; } = "Published";

    [SugarColumn(Length = 262144)]
    public string SchemaJson { get; set; } = "{}";
}

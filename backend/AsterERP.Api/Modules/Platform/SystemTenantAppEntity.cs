using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Platform;

[SugarTable("system_tenant_apps")]
public sealed class SystemTenantAppEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string Status { get; set; } = "Enabled";

    [SugarColumn(IsNullable = true)]
    public string? SystemName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? LogoFileId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? FaviconFileId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PrimaryColor { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ExpiredAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ConfigJson { get; set; }
}

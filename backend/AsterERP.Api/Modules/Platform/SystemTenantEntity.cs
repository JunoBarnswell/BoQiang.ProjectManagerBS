using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Platform;

[SugarTable("system_tenants")]
public sealed class SystemTenantEntity : EntityBase
{
    public string TenantCode { get; set; } = string.Empty;

    public string TenantName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ShortName { get; set; }

    public string Status { get; set; } = "Enabled";

    [SugarColumn(IsNullable = true)]
    public DateTime? ExpiredAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ContactName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ContactPhone { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ConfigJson { get; set; }
}

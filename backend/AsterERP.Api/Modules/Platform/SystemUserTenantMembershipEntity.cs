using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Platform;

[SugarTable("system_user_tenant_memberships")]
public sealed class SystemUserTenantMembershipEntity : EntityBase
{
    public string UserId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? DeptId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PositionId { get; set; }

    public bool IsTenantAdmin { get; set; }

    public bool IsDefault { get; set; }

    public string Status { get; set; } = "Enabled";
}

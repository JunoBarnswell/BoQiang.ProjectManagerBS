using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Roles;

[SugarTable("system_roles")]
public sealed class SystemRoleEntity : EntityBase
{
    [SugarColumn(IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AppCode { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string RoleCode { get; set; } = string.Empty;

    public string DataScope { get; set; } = "ALL";

    public bool IsEnabled { get; set; } = true;
}

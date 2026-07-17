using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Roles;

[SugarTable("system_role_permissions")]
public sealed class SystemRolePermissionEntity : EntityBase
{
    public string RoleId { get; set; } = string.Empty;

    public string PermissionCodeId { get; set; } = string.Empty;
}

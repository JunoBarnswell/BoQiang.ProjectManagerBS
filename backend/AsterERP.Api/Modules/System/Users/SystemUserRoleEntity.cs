using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Users;

[SugarTable("system_user_roles")]
public sealed class SystemUserRoleEntity : EntityBase
{
    public string UserId { get; set; } = string.Empty;

    public string RoleId { get; set; } = string.Empty;
}

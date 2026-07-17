using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Platform;

[SugarTable("system_user_app_roles")]
public sealed class SystemUserAppRoleEntity : EntityBase
{
    public string UserId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string RoleId { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}

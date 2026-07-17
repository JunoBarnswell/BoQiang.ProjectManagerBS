using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Permissions;

[SugarTable("system_permission_codes")]
public sealed class SystemPermissionCodeEntity : EntityBase
{
    public string ModuleName { get; set; } = string.Empty;

    public string PermissionCode { get; set; } = string.Empty;

    public string PermissionName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}

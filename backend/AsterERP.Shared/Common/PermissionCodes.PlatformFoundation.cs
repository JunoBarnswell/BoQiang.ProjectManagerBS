namespace AsterERP.Shared;

public static partial class PermissionCodes
{
    public const string PlatformTenantQuery = "platform:tenant:query";
    public const string PlatformTenantAdd = "platform:tenant:add";
    public const string PlatformTenantEdit = "platform:tenant:edit";
    public const string PlatformTenantEnable = "platform:tenant:enable";
    public const string PlatformTenantDisable = "platform:tenant:disable";
    public const string PlatformTenantDelete = "platform:tenant:delete";

    public const string PlatformApplicationQuery = "platform:application:query";
    public const string PlatformApplicationAdd = "platform:application:add";
    public const string PlatformApplicationEdit = "platform:application:edit";
    public const string PlatformApplicationEnable = "platform:application:enable";
    public const string PlatformApplicationDisable = "platform:application:disable";
    public const string PlatformApplicationDelete = "platform:application:delete";
    public const string PlatformApplicationEnter = "platform:application:enter";

    public const string PlatformTenantAppQuery = "platform:tenant-app:query";
    public const string PlatformTenantAppInstall = "platform:tenant-app:install";
    public const string PlatformTenantAppEnable = "platform:tenant-app:enable";
    public const string PlatformTenantAppDisable = "platform:tenant-app:disable";
    public const string PlatformTenantAppUninstall = "platform:tenant-app:uninstall";

    public const string PlatformUserTenantQuery = "platform:user-tenant:query";
    public const string PlatformUserTenantEdit = "platform:user-tenant:edit";
    public const string PlatformUserAppRoleQuery = "platform:user-app-role:query";
    public const string PlatformUserAppRoleEdit = "platform:user-app-role:edit";
}

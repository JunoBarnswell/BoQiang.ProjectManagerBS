namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPermissionOptionsResponse
{
    public List<ApplicationDevelopmentMenuOptionDto> MenuOptions { get; set; } = [];

    public List<ApplicationDevelopmentRoleOptionDto> RoleOptions { get; set; } = [];
}

namespace AsterERP.Contracts.System.Roles;

public sealed record RolePermissionCatalogItemResponse(
    string ModuleName,
    string PermissionCode,
    string PermissionName,
    bool IsEnabled);

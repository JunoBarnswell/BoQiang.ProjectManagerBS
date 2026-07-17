namespace AsterERP.Contracts.System.Roles;

public sealed record RolePermissionUpdateRequest(
    IReadOnlyList<string> PermissionCodes);

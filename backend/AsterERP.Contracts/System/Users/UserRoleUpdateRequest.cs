namespace AsterERP.Contracts.System.Users;

public sealed record UserRoleUpdateRequest(
    IReadOnlyList<string> RoleIds);

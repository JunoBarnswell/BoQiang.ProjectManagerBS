namespace AsterERP.Contracts.Auth;

public sealed record SwitchWorkspaceRequest(
    string TenantId,
    string AppCode);

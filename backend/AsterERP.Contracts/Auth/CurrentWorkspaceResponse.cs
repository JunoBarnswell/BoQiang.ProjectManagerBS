namespace AsterERP.Contracts.Auth;

public sealed record CurrentWorkspaceResponse(
    string TenantId,
    string TenantName,
    string AppCode,
    string AppName,
    string WorkspaceId = "",
    string SystemId = "",
    string SystemCode = "",
    string SystemName = "",
    string WorkspaceLevel = "platform",
    string? DefaultRoutePath = null);

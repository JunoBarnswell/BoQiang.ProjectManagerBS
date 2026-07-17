namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleApplicationResponse(
    string TenantId,
    string TenantName,
    string AppCode,
    string AppName,
    string SystemName,
    string? Version,
    string? DefaultRoutePath,
    string Status,
    string AppType,
    DateTime CreatedTime,
    DateTime? UpdatedTime,
    string WorkspaceLevel);

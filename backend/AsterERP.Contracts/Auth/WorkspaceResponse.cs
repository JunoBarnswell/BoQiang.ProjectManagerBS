namespace AsterERP.Contracts.Auth;

public sealed record WorkspaceResponse(
    string WorkspaceId,
    string TenantId,
    string TenantName,
    string AppCode,
    string AppName,
    string? LogoFileId,
    bool IsDefault,
    string SystemId = "",
    string SystemCode = "",
    string SystemName = "",
    string? Description = null,
    string Status = "Enabled",
    bool IsAvailable = true,
    string? DisabledReason = null,
    string WorkspaceLevel = "platform",
    string? DefaultRoutePath = null,
    bool IsDatabaseBound = true,
    bool CanManageInitialDatabaseBinding = false);

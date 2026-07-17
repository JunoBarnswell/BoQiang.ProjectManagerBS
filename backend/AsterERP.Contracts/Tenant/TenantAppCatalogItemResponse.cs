namespace AsterERP.Contracts.Tenant;

public sealed record TenantAppCatalogItemResponse(
    string AppCode,
    string AppName,
    string AppType,
    string? Icon,
    string? DefaultRoutePath,
    string? Version,
    bool Installed,
    string? TenantAppId,
    string? TenantAppStatus,
    string? SystemName,
    string? PrimaryColor,
    DateTime? ExpiredAt);

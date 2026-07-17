namespace AsterERP.Contracts.Platform;

public sealed record TenantAppListItemResponse(
    string Id,
    string TenantId,
    string TenantName,
    string AppCode,
    string AppName,
    string Status,
    string? SystemName,
    string? LogoFileId,
    string? FaviconFileId,
    string? PrimaryColor,
    DateTime? ExpiredAt,
    string? ConfigJson,
    string? Remark);

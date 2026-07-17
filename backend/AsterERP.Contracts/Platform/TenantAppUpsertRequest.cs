namespace AsterERP.Contracts.Platform;

public sealed record TenantAppUpsertRequest(
    string TenantId,
    string AppCode,
    string Status,
    string? SystemName,
    string? LogoFileId,
    string? FaviconFileId,
    string? PrimaryColor,
    DateTime? ExpiredAt,
    string? ConfigJson,
    string? Remark);

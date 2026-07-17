namespace AsterERP.Contracts.Tenant;

public sealed record TenantAppInstallRequest(
    string? SystemName,
    string? LogoFileId,
    string? FaviconFileId,
    string? PrimaryColor,
    DateTime? ExpiredAt,
    string? ConfigJson,
    string? Remark);

namespace AsterERP.Contracts.Platform;

public sealed record TenantListItemResponse(
    string Id,
    string TenantCode,
    string TenantName,
    string? ShortName,
    string Status,
    DateTime? ExpiredAt,
    string? ContactName,
    string? ContactPhone,
    string? ConfigJson,
    string? Remark);

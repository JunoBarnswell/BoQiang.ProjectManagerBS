namespace AsterERP.Contracts.Platform;

public sealed record TenantUpsertRequest(
    string TenantCode,
    string TenantName,
    string? ShortName,
    string Status,
    DateTime? ExpiredAt,
    string? ContactName,
    string? ContactPhone,
    string? ConfigJson,
    string? Remark);

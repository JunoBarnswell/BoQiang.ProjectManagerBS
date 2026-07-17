namespace AsterERP.Contracts.Platform;

public sealed record ApplicationBackendEntryRequest(
    string TenantId,
    string? Source = null);

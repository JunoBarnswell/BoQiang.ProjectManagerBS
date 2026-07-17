namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceTableRowConcurrencyResponse(
    string Strategy,
    string? VersionColumn,
    object? VersionValue,
    bool CanRetry,
    bool CanOverwrite);

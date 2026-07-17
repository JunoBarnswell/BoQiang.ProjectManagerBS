namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceWorkbenchResponse(
    ApplicationDataCenterObjectDetailResponse DataSource,
    bool IsDatabase,
    string? Endpoint,
    string? LastValidationStatus,
    string? LastValidationMessage,
    DateTime? LastValidatedAt,
    ApplicationDataSourceWorkbenchStatsResponse Stats);

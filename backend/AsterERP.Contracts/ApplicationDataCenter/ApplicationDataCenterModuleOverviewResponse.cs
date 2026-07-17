namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterModuleOverviewResponse(
    string ModuleKey,
    string Title,
    string Description,
    string ViewPermissionCode,
    int TotalCount,
    int PublishedCount,
    int WarningCount,
    int ErrorCount);

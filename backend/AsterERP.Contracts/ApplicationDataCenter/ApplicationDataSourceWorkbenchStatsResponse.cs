namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceWorkbenchStatsResponse(
    int TableCount,
    int ViewCount,
    int MicroflowCount,
    int MappingCacheCount,
    int ConnectionRunCount,
    int IntegrationTaskCount);

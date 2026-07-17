namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceRuntimeCheckResponse(
    IReadOnlyList<ApplicationConnectionCheckRunResponse> ConnectionRuns,
    ApplicationDataSourceWorkbenchStatsResponse Stats);

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterRuntimeQuerySortRequest(
    string FieldResourceId,
    string Direction = "asc");

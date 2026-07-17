namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationQueryPlanColumn(
    string FieldResourceId,
    string? Alias = null,
    string? NodeId = null,
    string? Aggregate = null,
    string? Function = null);

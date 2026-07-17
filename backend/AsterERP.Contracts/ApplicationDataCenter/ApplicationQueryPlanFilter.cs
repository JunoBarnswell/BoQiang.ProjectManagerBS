namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationQueryPlanFilter(
    string FieldResourceId,
    string Operator,
    string ParameterResourceId,
    string? NodeId = null);

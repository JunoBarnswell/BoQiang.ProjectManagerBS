namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterRuntimeQueryFilterRequest(
    string FieldResourceId,
    string Operator,
    object? Value);

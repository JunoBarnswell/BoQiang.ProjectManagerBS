namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed record ApplicationQueryPlanResolvedParameter(
    string ResourceId,
    string Name,
    string Type,
    bool Required,
    object? DefaultValue,
    string ColumnResourceId,
    string ColumnName);

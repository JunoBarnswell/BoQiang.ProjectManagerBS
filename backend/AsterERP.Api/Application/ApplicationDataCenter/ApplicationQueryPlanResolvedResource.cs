namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed record ApplicationQueryPlanResolvedResource(
    string ResourceId,
    string Kind,
    string? SchemaName,
    string ObjectName,
    IReadOnlyList<ApplicationQueryPlanResolvedField> Fields,
    IReadOnlyList<ApplicationQueryPlanResolvedParameter> Parameters,
    string SourceResourceId);

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed record ApplicationQueryPlanResolvedField(
    string ResourceId,
    string Name,
    string DataType,
    bool Nullable,
    string SourceResourceId,
    string SourceName);

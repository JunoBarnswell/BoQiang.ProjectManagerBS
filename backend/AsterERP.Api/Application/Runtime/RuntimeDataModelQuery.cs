namespace AsterERP.Api.Application.Runtime;

public sealed record RuntimeDataModelQuery(
    int PageIndex,
    int PageSize,
    string? Keyword,
    IReadOnlyList<RuntimeDataModelFilter> Filters,
    IReadOnlyList<RuntimeDataModelSort> Sorts);

public sealed record RuntimeDataModelFilter(
    RuntimeDataFieldDefinition Field,
    string Operator,
    object? Value,
    object? ValueTo);

public sealed record RuntimeDataModelSort(
    RuntimeDataFieldDefinition Field,
    string Order);

public sealed record RuntimeDataModelQueryResult(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int Total);

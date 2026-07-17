namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeDataFieldResponse(
    string FieldCode,
    string FieldName,
    string DataType,
    string Binding,
    bool Visible,
    bool Queryable,
    bool Sortable,
    bool Exportable,
    bool Writable,
    string? Renderer,
    string? DictType,
    string? Width,
    string? Fixed,
    int Order,
    bool Required = false,
    IReadOnlyList<RuntimeExpressionHelperDto>? DisplayHelpers = null,
    IReadOnlyList<RuntimeExpressionHelperDto>? WriteHelpers = null,
    IReadOnlyList<RuntimeExpressionHelperDto>? QueryHelpers = null);

public sealed record RuntimeFilterRequest(
    string Field,
    string Operator,
    object? Value,
    object? ValueTo);

public sealed record RuntimeSortRequest(
    string Field,
    string Order);

public sealed record RuntimeQueryRequest(
    int PageIndex,
    int PageSize,
    string? Keyword,
    IReadOnlyList<RuntimeFilterRequest>? Filters,
    IReadOnlyList<RuntimeSortRequest>? Sorts,
    string? PageCode = null,
    string? PreviewPageId = null);

public sealed record RuntimeCellSpanResponse(
    int RowIndex,
    string ColumnKey,
    int RowSpan,
    int ColSpan);

public sealed record RuntimeQueryResponse(
    IReadOnlyList<RuntimeDataFieldResponse> Fields,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int Total,
    int PageIndex,
    int PageSize,
    IReadOnlyList<RuntimeCellSpanResponse>? CellSpans = null,
    string? KeyField = null);

public sealed record RuntimeDetailResponse(
    IReadOnlyList<RuntimeDataFieldResponse> Fields,
    IReadOnlyDictionary<string, object?> Row);

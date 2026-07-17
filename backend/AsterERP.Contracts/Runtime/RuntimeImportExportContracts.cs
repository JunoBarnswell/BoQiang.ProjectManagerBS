namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeImportPreviewRowResponse(
    int RowNumber,
    IReadOnlyDictionary<string, object?> Values,
    IReadOnlyList<string> Errors);

public sealed record RuntimeImportPreviewResponse(
    string ModelCode,
    string PageCode,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    IReadOnlyList<RuntimeDataFieldResponse> Fields,
    IReadOnlyList<RuntimeImportPreviewRowResponse> Rows);

public sealed record RuntimeImportResponse(
    string ModelCode,
    string PageCode,
    int TotalRows,
    int CreatedRows,
    int FailedRows,
    IReadOnlyList<RuntimeImportPreviewRowResponse> Errors);

public sealed record RuntimeExportRequest(
    int PageIndex,
    int PageSize,
    string? Keyword,
    IReadOnlyList<RuntimeFilterRequest>? Filters,
    IReadOnlyList<RuntimeSortRequest>? Sorts,
    string? PageCode,
    IReadOnlyList<string>? Columns);

public sealed record RuntimeExportResponse(
    string FileName,
    string ContentType,
    string ContentBase64,
    int TotalRows);

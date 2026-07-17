namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeGridValueSourceResponse(
    string Type,
    string? Field,
    string? Template,
    IReadOnlyList<string>? Fields,
    string? Path);

public sealed record RuntimeGridMergeResponse(
    bool Enabled,
    string Direction,
    string Strategy,
    IReadOnlyList<string> Fields);

public sealed record RuntimeGridViewColumnResponse(
    string Key,
    string? Title,
    string? Binding,
    string? Width,
    string? Fixed,
    bool? IsVisible,
    int? Order,
    string? Renderer,
    string? QueryField,
    string? SortField,
    RuntimeGridValueSourceResponse? ValueSource,
    RuntimeGridMergeResponse? Merge,
    IReadOnlyList<RuntimeGridViewColumnResponse>? Children);

public sealed record RuntimeGridViewResponse(
    string PageCode,
    string Source,
    IReadOnlyList<RuntimeGridViewColumnResponse> Columns,
    string? TenantViewJson,
    string? UserViewJson);

public sealed record RuntimeGridViewSaveRequest(
    IReadOnlyList<RuntimeGridViewColumnResponse> Columns);

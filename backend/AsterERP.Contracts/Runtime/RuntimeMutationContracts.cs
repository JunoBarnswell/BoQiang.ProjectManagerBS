namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCreateRequest(
    IReadOnlyDictionary<string, object?> Values,
    string? PageCode = null,
    string? PreviewPageId = null);

public sealed record RuntimeUpdateRequest(
    IReadOnlyDictionary<string, object?> Values,
    string? PageCode = null,
    string? PreviewPageId = null);

public sealed record RuntimeMutationResponse(
    string Id,
    IReadOnlyDictionary<string, object?>? Row,
    bool Success);

public sealed record RuntimeCreateResponse(string Id, IReadOnlyDictionary<string, object?> Row);

public sealed record RuntimeDeleteResponse(string Id, bool Deleted);

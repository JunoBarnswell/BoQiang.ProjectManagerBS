namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeUpdateRequest(
    string RootModelCode,
    string RootId,
    IReadOnlyDictionary<string, object?> RootValues,
    IReadOnlyList<RuntimeCompositeChildUpdateRequest> Children,
    string? PageCode = null,
    string? PreviewPageId = null);

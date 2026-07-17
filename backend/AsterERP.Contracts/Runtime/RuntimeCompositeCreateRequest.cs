namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeCreateRequest(
    string RootModelCode,
    IReadOnlyDictionary<string, object?> RootValues,
    IReadOnlyList<RuntimeCompositeChildCreateRequest> Children,
    string? PageCode = null,
    string? PreviewPageId = null);

namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeDeleteRequest(
    string RootModelCode,
    string RootId,
    IReadOnlyList<RuntimeCompositeChildDeleteRequest> Children,
    string? PageCode = null,
    string? PreviewPageId = null);

namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeDetailRequest(
    string RootModelCode,
    string RootId,
    IReadOnlyList<RuntimeCompositeChildDetailRequest> Children,
    string? PageCode = null,
    string? PreviewPageId = null);

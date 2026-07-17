namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeDeleteResponse(
    RuntimeDeleteResponse Root,
    IReadOnlyList<RuntimeCompositeChildDeleteResponse> Children);

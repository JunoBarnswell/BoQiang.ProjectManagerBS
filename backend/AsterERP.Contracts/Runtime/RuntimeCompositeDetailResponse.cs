namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeDetailResponse(
    RuntimeDetailResponse Root,
    IReadOnlyList<RuntimeCompositeChildDetailResponse> Children);

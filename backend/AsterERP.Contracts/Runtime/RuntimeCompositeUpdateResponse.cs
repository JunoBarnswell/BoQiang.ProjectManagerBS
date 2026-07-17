namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeUpdateResponse(
    RuntimeMutationResponse Root,
    IReadOnlyList<RuntimeCompositeChildUpdateResponse> Children);

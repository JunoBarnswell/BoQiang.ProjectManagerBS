namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeCreateResponse(
    RuntimeCreateResponse Root,
    IReadOnlyList<RuntimeCompositeChildCreateResponse> Children);

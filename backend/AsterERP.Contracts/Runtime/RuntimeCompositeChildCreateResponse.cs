namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeChildCreateResponse(
    string ModelCode,
    IReadOnlyList<RuntimeCreateResponse> Rows);

namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeChildUpdateResponse(
    string ModelCode,
    IReadOnlyList<RuntimeCreateResponse> CreatedRows,
    IReadOnlyList<RuntimeMutationResponse> UpdatedRows,
    int DeletedCount);

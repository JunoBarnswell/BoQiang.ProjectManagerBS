namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeChildDeleteResponse(
    string ModelCode,
    int DeletedCount);

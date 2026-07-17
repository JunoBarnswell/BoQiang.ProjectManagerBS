namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeChildCreateRequest(
    string ModelCode,
    string ParentKeyField,
    string ForeignKeyField,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);

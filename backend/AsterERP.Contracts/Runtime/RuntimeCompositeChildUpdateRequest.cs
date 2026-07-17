namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeChildUpdateRequest(
    string ModelCode,
    string ParentKeyField,
    string ForeignKeyField,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyList<string>? DeleteIds = null,
    bool DeleteMissing = false);

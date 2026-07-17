namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeChildDeleteRequest(
    string ModelCode,
    string ParentKeyField = "id",
    string ForeignKeyField = "",
    string? ParentId = null,
    bool Required = false);

namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeChildDetailRequest(
    string ModelCode,
    string? ParentKeyField,
    string ForeignKeyField,
    RuntimeQueryRequest? Query = null,
    string? BindingKey = null);

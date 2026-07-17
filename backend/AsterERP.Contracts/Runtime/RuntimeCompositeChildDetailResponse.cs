namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeCompositeChildDetailResponse(
    string ModelCode,
    string? BindingKey,
    string? ParentKeyField,
    string ForeignKeyField,
    RuntimeQueryResponse Data);

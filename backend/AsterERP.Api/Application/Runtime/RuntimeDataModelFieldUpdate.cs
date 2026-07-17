namespace AsterERP.Api.Application.Runtime;

public sealed record RuntimeDataModelFieldUpdate(
    RuntimeDataFieldDefinition Field,
    object? Value);

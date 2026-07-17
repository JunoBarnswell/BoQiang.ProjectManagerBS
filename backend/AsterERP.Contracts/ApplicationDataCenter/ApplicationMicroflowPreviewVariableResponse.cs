namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMicroflowPreviewVariableResponse(
    string Name,
    string ValueType,
    string DisplayValue,
    string? DatasetKey);

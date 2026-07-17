namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMicroflowPreviewRequest(
    string? Mode = null,
    ApplicationMicroflowExecuteRequest? ExecuteRequest = null,
    string? DraftConfigJson = null,
    int? MaxRows = null,
    string? PreferredResultPath = null);

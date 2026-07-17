namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceDraftDiagnosticResponse(
    bool Success,
    IReadOnlyList<ApplicationConnectionDiagnosticStageResponse> Stages,
    string? ConnectionFingerprint = null);

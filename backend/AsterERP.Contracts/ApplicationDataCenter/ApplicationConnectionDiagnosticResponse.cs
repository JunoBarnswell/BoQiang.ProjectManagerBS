namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationConnectionDiagnosticResponse(
    string TaskId,
    bool Success,
    IReadOnlyList<ApplicationConnectionDiagnosticStageResponse> Stages,
    string? ConnectionFingerprint = null);

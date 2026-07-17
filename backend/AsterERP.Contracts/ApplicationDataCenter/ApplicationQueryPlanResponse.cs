namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationQueryPlanResponse(
    ApplicationDataCenterPreviewResponse Data,
    ApplicationQueryPlanDiagnosticResponse Plan,
    int Total,
    string AuditId,
    string? RequestHash = null,
    string? LedgerId = null,
    string? ExecutionStatus = null,
    bool RecoveryRequired = false);

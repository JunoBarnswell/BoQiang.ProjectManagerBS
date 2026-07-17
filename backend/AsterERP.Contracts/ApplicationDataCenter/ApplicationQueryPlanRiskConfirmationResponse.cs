namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationQueryPlanRiskConfirmationResponse(
    string RiskConfirmationId,
    string RequestHash,
    DateTime ExpiresAt);

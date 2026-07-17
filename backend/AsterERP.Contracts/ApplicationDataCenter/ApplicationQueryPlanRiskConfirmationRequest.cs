namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationQueryPlanRiskConfirmationRequest
{
    public ApplicationQueryPlanRequest Plan { get; set; } = new();

    public int ExpiresInSeconds { get; set; } = 120;
}

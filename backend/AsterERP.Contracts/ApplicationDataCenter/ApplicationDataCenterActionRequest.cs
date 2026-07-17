namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataCenterActionRequest
{
    public string? ParametersJson { get; set; }

    public IReadOnlyList<string> ConfirmedRiskFields { get; set; } = [];
}

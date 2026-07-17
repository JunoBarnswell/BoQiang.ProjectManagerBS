namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataCenterPublishRequest
{
    public string? Remark { get; set; }

    public IReadOnlyList<string> ConfirmedRiskFields { get; set; } = [];
}

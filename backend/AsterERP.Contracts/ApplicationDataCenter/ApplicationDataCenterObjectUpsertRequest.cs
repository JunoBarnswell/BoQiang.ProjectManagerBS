namespace AsterERP.Contracts.ApplicationDataCenter;

public class ApplicationDataCenterObjectUpsertRequest
{
    public string ObjectCode { get; set; } = string.Empty;

    public string ObjectName { get; set; } = string.Empty;

    public string ObjectType { get; set; } = string.Empty;

    public string? Environment { get; set; }

    public string? Endpoint { get; set; }

    public string? OwnerUserId { get; set; }

    public string ConfigJson { get; set; } = "{}";

    public string? SecretConfigJson { get; set; }

    public string? Remark { get; set; }

    public IReadOnlyList<string> ConfirmedRiskFields { get; set; } = [];
}

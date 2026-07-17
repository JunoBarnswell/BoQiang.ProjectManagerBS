namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataCenterPreviewRequest
{
    public string? ParametersJson { get; set; }

    public int MaxRows { get; set; } = 20;
}

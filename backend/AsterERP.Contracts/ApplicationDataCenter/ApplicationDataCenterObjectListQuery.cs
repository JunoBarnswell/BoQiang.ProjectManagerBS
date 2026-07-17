namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataCenterObjectListQuery
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public string? ObjectType { get; set; }

    public string? Status { get; set; }

    public string? OwnerUserId { get; set; }
}

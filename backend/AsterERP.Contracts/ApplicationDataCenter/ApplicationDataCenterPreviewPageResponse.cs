namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterPreviewPageResponse(
    int PageIndex,
    int PageSize,
    int TotalRows,
    bool HasPrevious,
    bool HasNext);

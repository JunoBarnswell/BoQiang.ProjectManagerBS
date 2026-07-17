namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleCapabilityCountsResponse(
    int RootMenuCount,
    int MenuCount,
    int PermissionCount,
    int PageCount,
    int PublishedPageCount,
    int DataModelCount,
    int WorkflowModelCount,
    int PublishTaskCount,
    int DraftPageCount,
    int PreviewMenuCount,
    int DraftVersionCount,
    int PublishedVersionCount);

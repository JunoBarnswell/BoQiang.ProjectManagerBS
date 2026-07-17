namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleRecentDevelopmentItemResponse(
    string Id,
    string PageId,
    string Title,
    string PageCode,
    string Status,
    string Description,
    string? ModuleName,
    string? ModuleCode,
    string? VersionId,
    string? VersionName,
    string? VersionCode,
    string ContinueRoutePath,
    string? PreviewRoutePath,
    bool CanContinueDesign,
    bool CanPreview,
    bool CanPublish,
    DateTime UpdatedTime,
    string VisitKind);

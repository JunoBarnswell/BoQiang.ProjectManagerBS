using AsterERP.Shared;

namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementRecycleQuery(
    int PageIndex = 1,
    int PageSize = 20,
    string? ProjectId = null,
    string? Keyword = null);

public sealed record ProjectManagementRecycleProjectItem(
    string Id,
    string ProjectCode,
    string ProjectName,
    string Status,
    long VersionNo,
    DateTime? DeletedTime,
    string? DeletedBy,
    bool CanRestore,
    bool CanPurge);

public sealed record ProjectManagementRecycleTaskItem(
    string Id,
    string ProjectId,
    string TaskCode,
    string Title,
    string Status,
    long VersionNo,
    DateTime? DeletedTime,
    string? DeletedBy,
    bool CanRestore,
    bool CanPurge);

public sealed record ProjectManagementRecycleResponse(
    GridPageResult<ProjectManagementRecycleProjectItem> Projects,
    GridPageResult<ProjectManagementRecycleTaskItem> Tasks);

public sealed record ProjectManagementRecycleRestoreRequest(long VersionNo, bool RestoreDescendants = false);

public sealed record ProjectManagementRecyclePurgeRequest(long VersionNo, string CurrentPassword, bool ConfirmRisk);

public sealed record ProjectManagementRecyclePurgePreviewResponse(
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    long VersionNo,
    int MemberReferenceCount,
    int MilestoneReferenceCount,
    int TaskReferenceCount,
    bool CanExecute,
    string? BlockingReason,
    string RollbackHint);

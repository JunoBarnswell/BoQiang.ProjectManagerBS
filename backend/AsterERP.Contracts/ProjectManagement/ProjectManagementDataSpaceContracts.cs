namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementDataSpaceSummaryResponse(
    string TenantId,
    string AppCode,
    string DatabaseStatus,
    int ProjectCount,
    int TaskCount,
    int MemberCount,
    int MilestoneCount,
    int AttachmentCount,
    DateTime? LastActivityTime,
    string DataSpaceName,
    string DatabaseBindingStatus,
    string? StatusMessage,
    string? HandlingRoute,
    bool IsStatisticsScoped,
    DateTime? LastBackupTime,
    IReadOnlyList<ProjectManagementDataSpaceOptionResponse> AvailableDataSpaces);

/// <summary>
/// 当前用户可以进入的数据空间。此契约仅包含工作区切换所需的公开状态，绝不返回连接字符串、数据库名称或文件路径。
/// </summary>
public sealed record ProjectManagementDataSpaceOptionResponse(
    string WorkspaceId,
    string TenantId,
    string TenantName,
    string AppCode,
    string AppName,
    string Status,
    bool IsAvailable,
    bool IsDatabaseBound,
    bool IsCurrent,
    string? DisabledReason,
    string? HandlingRoute);

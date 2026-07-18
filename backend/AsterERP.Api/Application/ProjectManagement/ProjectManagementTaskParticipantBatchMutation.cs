namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 供任务批量聚合在同一数据库事务内调用的参与人替换命令。
/// 项目级权限、任务版本与外层事务由调用方负责；本命令只拥有参与人领域约束。
/// </summary>
public sealed record ProjectManagementTaskParticipantBatchReplaceRequest(
    string ProjectId,
    IReadOnlyList<ProjectManagementTaskParticipantBatchReplaceItem> Items,
    string? TraceId = null);

public sealed record ProjectManagementTaskParticipantBatchReplaceItem(
    string TaskId,
    IReadOnlyCollection<string> ParticipantUserIds);

public sealed record ProjectManagementTaskParticipantBatchMutationResult(
    string ProjectId,
    string TraceId,
    IReadOnlyList<ProjectManagementTaskParticipantBatchTaskResult> Tasks,
    IReadOnlyList<ProjectManagementTaskParticipantBatchNotification> Notifications,
    IReadOnlyList<string> ConversationSyncTaskIds);

public sealed record ProjectManagementTaskParticipantBatchTaskResult(
    string TaskId,
    IReadOnlyList<string> AddedUserIds,
    IReadOnlyList<string> RemovedUserIds,
    IReadOnlyList<string> UnchangedUserIds);

public sealed record ProjectManagementTaskParticipantBatchNotification(
    string TaskId,
    string ProjectId,
    string TaskTitle,
    string RecipientUserId,
    string NotificationType,
    string Title,
    string Message,
    string TraceId);

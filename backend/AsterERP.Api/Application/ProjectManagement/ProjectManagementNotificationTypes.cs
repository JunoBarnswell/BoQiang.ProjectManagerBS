namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 项目域通知的稳定类型标识。类型是持久化和筛选契约，显示文案由调用方按业务上下文提供。
/// </summary>
public static class ProjectManagementNotificationTypes
{
    public const string TaskReminder = "task.reminder";
    public const string TaskMentioned = "task.comment.mentioned";
    public const string TaskAssigned = "task.assigned";
    public const string TaskParticipantAdded = "task.participant.added";
    public const string TaskParticipantRemoved = "task.participant.removed";
    public const string TaskStatusChanged = "task.status.changed";
    public const string TaskDueDateChanged = "task.due-date.changed";
    public const string MilestoneRiskDetected = "milestone.risk.detected";
    public const string ExcelImportCompleted = "project.excel-import";
    public const string SyncExportCompleted = "sync.export";
    public const string SyncImportCompleted = "sync.import";
    public const string SyncImportFailed = "sync.import.failed";
    public const string OperationSucceeded = "operation.succeeded";
    public const string OperationFailed = "operation.failed";
}

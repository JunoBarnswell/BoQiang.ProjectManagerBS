namespace AsterERP.Shared;

public static partial class PermissionCodes
{
    public const string ProjectManagementProjectView = "project-management:project:view";
    public const string ProjectManagementProjectAdd = "project-management:project:add";
    public const string ProjectManagementProjectEdit = "project-management:project:edit";
    public const string ProjectManagementProjectArchive = "project-management:project:archive";
    public const string ProjectManagementProjectDelete = "project-management:project:delete";
    public const string ProjectManagementProjectRestore = "project-management:project:restore";
    public const string ProjectManagementProjectPurge = "project-management:project:purge";

    public const string ProjectManagementMemberView = "project-management:member:view";
    public const string ProjectManagementMemberManage = "project-management:member:manage";
    public const string ProjectManagementMilestoneView = "project-management:milestone:view";
    public const string ProjectManagementMilestoneManage = "project-management:milestone:manage";

    public const string ProjectManagementTaskView = "project-management:task:view";
    public const string ProjectManagementTaskAdd = "project-management:task:add";
    public const string ProjectManagementTaskEdit = "project-management:task:edit";
    public const string ProjectManagementTaskDelete = "project-management:task:delete";
    public const string ProjectManagementTaskPurge = "project-management:task:purge";
    public const string ProjectManagementTaskRestore = "project-management:task:restore";
    public const string ProjectManagementTaskMove = "project-management:task:move";
    public const string ProjectManagementTaskAssign = "project-management:task:assign";
    public const string ProjectManagementTaskManageDependency = "project-management:task:manage-dependency";
    public const string ProjectManagementTaskOverrideWip = "project-management:task:override-wip";
    public const string ProjectManagementLabelView = "project-management:label:view";
    public const string ProjectManagementLabelManage = "project-management:label:manage";
    public const string ProjectManagementTaskTemplateManage = "project-management:task-template:manage";

    public const string ProjectManagementCommentView = "project-management:comment:view";
    public const string ProjectManagementCommentAdd = "project-management:comment:add";
    public const string ProjectManagementNotificationView = "project-management:notification:view";
    public const string ProjectManagementImConversationView = "project-management:im-conversation:view";
    public const string ProjectManagementImConversationManage = "project-management:im-conversation:manage";
    public const string ProjectManagementAttachmentManage = "project-management:attachment:manage";
    public const string ProjectManagementReminderView = "project-management:reminder:view";
    public const string ProjectManagementReminderManage = "project-management:reminder:manage";
    public const string ProjectManagementReportExport = "project-management:report:export";
    public const string ProjectManagementSyncImport = "project-management:sync:import";
    public const string ProjectManagementSyncExport = "project-management:sync:export";
    public const string ProjectManagementBackupManage = "project-management:backup:manage";
    public const string ProjectManagementAuditView = "project-management:audit:view";
    public const string ProjectManagementAuditExport = "project-management:audit:export";
    public const string ProjectManagementOperationView = "project-management:operation:view";
    public const string ProjectManagementOperationManage = "project-management:operation:manage";

    public static readonly IReadOnlyList<string> ProjectManagementPermissionCodes =
    [
        ProjectManagementProjectView,
        ProjectManagementProjectAdd,
        ProjectManagementProjectEdit,
        ProjectManagementProjectArchive,
        ProjectManagementProjectDelete,
        ProjectManagementProjectRestore,
        ProjectManagementProjectPurge,
        ProjectManagementMemberView,
        ProjectManagementMemberManage,
        ProjectManagementMilestoneView,
        ProjectManagementMilestoneManage,
        ProjectManagementTaskView,
        ProjectManagementTaskAdd,
        ProjectManagementTaskEdit,
        ProjectManagementTaskDelete,
        ProjectManagementTaskPurge,
        ProjectManagementTaskRestore,
        ProjectManagementTaskMove,
        ProjectManagementTaskAssign,
        ProjectManagementTaskManageDependency,
        ProjectManagementTaskOverrideWip,
        ProjectManagementLabelView,
        ProjectManagementLabelManage,
        ProjectManagementTaskTemplateManage,
        ProjectManagementCommentView,
        ProjectManagementCommentAdd,
        ProjectManagementNotificationView,
        ProjectManagementImConversationView,
        ProjectManagementImConversationManage,
        ProjectManagementAttachmentManage,
        ProjectManagementReminderView,
        ProjectManagementReminderManage,
        ProjectManagementReportExport,
        ProjectManagementSyncImport,
        ProjectManagementSyncExport,
        ProjectManagementBackupManage,
        ProjectManagementAuditView,
        ProjectManagementAuditExport,
        ProjectManagementOperationView,
        ProjectManagementOperationManage
    ];
}

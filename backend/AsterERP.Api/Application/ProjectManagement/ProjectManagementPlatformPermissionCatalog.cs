using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// SYSTEM 平台项目管理入口使用的权限定义。
/// 应用数据库基线不再播种这些权限，避免应用管理员获得平台项目管理能力。
/// </summary>
public static class ProjectManagementPlatformPermissionCatalog
{
    public static readonly IReadOnlyList<ProjectManagementPlatformPermissionDefinition> Definitions =
    [
        new(PermissionCodes.ProjectManagementProjectView, "项目中心访问"),
        new(PermissionCodes.ProjectManagementProjectAdd, "项目新增"),
        new(PermissionCodes.ProjectManagementProjectEdit, "项目编辑"),
        new(PermissionCodes.ProjectManagementProjectArchive, "项目归档"),
        new(PermissionCodes.ProjectManagementProjectDelete, "项目删除"),
        new(PermissionCodes.ProjectManagementProjectRestore, "项目恢复"),
        new(PermissionCodes.ProjectManagementProjectPurge, "项目永久删除"),
        new(PermissionCodes.ProjectManagementMemberView, "项目成员查看"),
        new(PermissionCodes.ProjectManagementMemberManage, "项目成员管理"),
        new(PermissionCodes.ProjectManagementMilestoneView, "里程碑查看"),
        new(PermissionCodes.ProjectManagementMilestoneManage, "里程碑管理"),
        new(PermissionCodes.ProjectManagementTaskView, "任务查看"),
        new(PermissionCodes.ProjectManagementTaskAdd, "任务新增"),
        new(PermissionCodes.ProjectManagementTaskEdit, "任务编辑"),
        new(PermissionCodes.ProjectManagementTaskDelete, "任务删除"),
        new(PermissionCodes.ProjectManagementTaskPurge, "任务永久删除"),
        new(PermissionCodes.ProjectManagementTaskRestore, "任务恢复"),
        new(PermissionCodes.ProjectManagementTaskMove, "任务移动"),
        new(PermissionCodes.ProjectManagementTaskAssign, "任务分配"),
        new(PermissionCodes.ProjectManagementTaskManageDependency, "任务依赖管理"),
        new(PermissionCodes.ProjectManagementTaskOverrideWip, "任务 WIP 强制绕过"),
        new(PermissionCodes.ProjectManagementLabelView, "标签查看"),
        new(PermissionCodes.ProjectManagementLabelManage, "标签管理"),
        new(PermissionCodes.ProjectManagementTaskTemplateManage, "任务模板管理"),
        new(PermissionCodes.ProjectManagementCommentView, "评论查看"),
        new(PermissionCodes.ProjectManagementCommentAdd, "评论新增"),
        new(PermissionCodes.ProjectManagementNotificationView, "项目通知查看"),
        new(PermissionCodes.ProjectManagementImConversationView, "项目关联 IM 会话查看"),
        new(PermissionCodes.ProjectManagementImConversationManage, "项目关联 IM 会话管理"),
        new(PermissionCodes.ProjectManagementAttachmentManage, "附件管理"),
        new(PermissionCodes.ProjectManagementReminderView, "项目提醒查看"),
        new(PermissionCodes.ProjectManagementReminderManage, "项目提醒管理"),
        new(PermissionCodes.ProjectManagementReportExport, "项目报表导出"),
        new(PermissionCodes.ProjectManagementSyncImport, "项目同步导入"),
        new(PermissionCodes.ProjectManagementSyncExport, "项目同步导出"),
        new(PermissionCodes.ProjectManagementAuditView, "项目审计查看"),
        new(PermissionCodes.ProjectManagementAuditExport, "项目审计导出"),
        new(PermissionCodes.ProjectManagementOperationView, "项目长任务查看"),
        new(PermissionCodes.ProjectManagementOperationManage, "项目长任务维护"),
        new(PermissionCodes.ProjectManagementReversibleCommandView, "项目撤销重做查看"),
        new(PermissionCodes.ProjectManagementReversibleCommandManage, "项目撤销重做执行")
    ];
}

public sealed record ProjectManagementPlatformPermissionDefinition(
    string PermissionCode,
    string PermissionName);

using AsterERP.Shared;

namespace AsterERP.Api.Application.ApplicationConsole;

public static class ApplicationWorkflowShellMenuCatalog
{
    public static readonly IReadOnlyList<ApplicationShellMenuDefinition> Items =
    [
        new("workflow", "审批流", null, null, null, null, "Workflow", 8, "Directory"),
        new("workflow:workspace", "个人工作台", "workflow", null, null, null, "UserCheck", 1, "Directory"),
        new("workflow:management", "流程管理", "workflow", null, null, null, "GitBranch", 2, "Directory"),
        new("workflow:analytics", "统计报表", "workflow", null, null, null, "ChartNoAxesCombined", 3, "Directory"),
        new("workflow:settings", "系统与基础设置", "workflow", null, null, null, "Settings2", 4, "Directory"),

        new("workflow:initiate", "发起申请", "workflow:workspace", "/workflows/initiate", "WorkflowInitiatePage", PermissionCodes.WorkflowInstanceStart, "Send", 1),
        new("workflow:todo", "待办审批", "workflow:workspace", "/workflows/tasks?tab=todo", "WorkflowTasksPage", PermissionCodes.WorkflowTaskQuery, "ListChecks", 2),
        new("workflow:done", "已办审批", "workflow:workspace", "/workflows/tasks?tab=done", "WorkflowTasksPage", PermissionCodes.WorkflowTaskQuery, "CheckCheck", 3),
        new("workflow:mine", "我发起的", "workflow:workspace", "/workflows/tasks?tab=mine", "WorkflowTasksPage", PermissionCodes.WorkflowInstanceQuery, "FileClock", 4),
        new("workflow:cc", "抄送我的", "workflow:workspace", "/workflows/tasks?tab=cc", "WorkflowTasksPage", PermissionCodes.WorkflowTaskQuery, "AtSign", 5),
        new("workflow:drafts", "草稿箱", "workflow:workspace", "/workflows/drafts", "WorkflowDraftsPage", PermissionCodes.WorkflowDraftQuery, "FilePenLine", 6),
        new("workflow:history", "审批记录", "workflow:workspace", "/workflows/history", "WorkflowHistoryPage", PermissionCodes.WorkflowHistoryQuery, "History", 7),

        new("workflow:forms", "表单管理", "workflow:management", "/workflows/forms", "WorkflowFormsPage", PermissionCodes.WorkflowFormQuery, "FormInput", 1),
        new("workflow:models", "流程设计", "workflow:management", "/workflows/models", "WorkflowModelsPage", PermissionCodes.WorkflowModelQuery, "Workflow", 2),
        new("workflow:bindings", "审批配置", "workflow:management", "/workflows/bindings", "WorkflowBindingsPage", PermissionCodes.WorkflowBindingQuery, "GitPullRequestArrow", 3),
        new("workflow:categories", "流程分类", "workflow:management", "/workflows/categories", "WorkflowCategoriesPage", PermissionCodes.WorkflowCategoryQuery, "FolderTree", 4),
        new("workflow:monitoring", "流程监控", "workflow:management", "/workflows/monitoring", "WorkflowMonitoringPage", PermissionCodes.WorkflowInstanceQuery, "Radar", 5),

        new("workflow:report:approval", "审批统计", "workflow:analytics", "/workflows/reports?tab=approval", "WorkflowReportsPage", PermissionCodes.WorkflowReportQuery, "ChartColumn", 1),
        new("workflow:report:efficiency", "效率分析", "workflow:analytics", "/workflows/reports?tab=efficiency", "WorkflowReportsPage", PermissionCodes.WorkflowReportQuery, "Timer", 2),
        new("workflow:report:business", "业务数据", "workflow:analytics", "/workflows/reports?tab=business", "WorkflowReportsPage", PermissionCodes.WorkflowReportQuery, "DatabaseZap", 3),

        new("workflow:settings:org", "组织架构与用户", "workflow:settings", "/system/users?from=workflow", "UsersPage", PermissionCodes.SystemUserQuery, "Users", 1),
        new("workflow:settings:roles", "角色与权限组", "workflow:settings", "/system/roles?from=workflow", "RolesPage", PermissionCodes.SystemRoleQuery, "ShieldCheck", 2),
        new("workflow:delegations", "审批委托", "workflow:settings", "/workflows/delegations", "WorkflowDelegationsPage", PermissionCodes.WorkflowDelegationQuery, "UserRoundCog", 3),
        new("workflow:notifications", "消息通知设置", "workflow:settings", "/workflows/notifications", "WorkflowNotificationsPage", PermissionCodes.WorkflowNotificationTaskQuery, "BellRing", 4),
        new("workflow:calendars", "节假日/工作日历", "workflow:settings", "/workflows/calendars", "WorkflowCalendarsPage", PermissionCodes.WorkflowCalendarQuery, "CalendarDays", 5),

        new("workflow:model:add", "新增模型", "workflow:models", null, null, PermissionCodes.WorkflowModelAdd, "", 1, "Button"),
        new("workflow:model:edit", "编辑模型", "workflow:models", null, null, PermissionCodes.WorkflowModelEdit, "", 2, "Button"),
        new("workflow:model:delete", "删除模型", "workflow:models", null, null, PermissionCodes.WorkflowModelDelete, "", 3, "Button"),
        new("workflow:model:publish", "发布模型", "workflow:models", null, null, PermissionCodes.WorkflowModelPublish, "", 4, "Button"),
        new("workflow:model:suspend", "停用模型", "workflow:models", null, null, PermissionCodes.WorkflowModelSuspend, "", 5, "Button"),
        new("workflow:binding:edit", "保存审批配置", "workflow:bindings", null, null, PermissionCodes.WorkflowBindingEdit, "", 1, "Button"),
        new("workflow:binding:delete", "删除审批配置", "workflow:bindings", null, null, PermissionCodes.WorkflowBindingDelete, "", 2, "Button"),
        new("workflow:category:edit", "保存分类", "workflow:categories", null, null, PermissionCodes.WorkflowCategoryEdit, "", 1, "Button"),
        new("workflow:category:delete", "删除分类", "workflow:categories", null, null, PermissionCodes.WorkflowCategoryDelete, "", 2, "Button"),
        new("workflow:task:claim", "认领任务", "workflow:todo", null, null, PermissionCodes.WorkflowTaskClaim, "", 1, "Button"),
        new("workflow:task:approve", "审批任务", "workflow:todo", null, null, PermissionCodes.WorkflowTaskApprove, "", 2, "Button"),
        new("workflow:task:transfer", "转办任务", "workflow:todo", null, null, PermissionCodes.WorkflowTaskTransfer, "", 3, "Button"),
        new("workflow:task:delegate", "委派任务", "workflow:todo", null, null, PermissionCodes.WorkflowTaskDelegate, "", 4, "Button"),
        new("workflow:task:attachment", "附件", "workflow:todo", null, null, PermissionCodes.WorkflowTaskAttachment, "", 5, "Button"),
        new("workflow:task:comment", "评论", "workflow:todo", null, null, PermissionCodes.WorkflowTaskComment, "", 6, "Button"),
        new("workflow:draft:edit", "保存草稿", "workflow:drafts", null, null, PermissionCodes.WorkflowDraftEdit, "", 1, "Button"),
        new("workflow:draft:submit", "提交草稿", "workflow:drafts", null, null, PermissionCodes.WorkflowDraftSubmit, "", 2, "Button"),
        new("workflow:draft:delete", "删除草稿", "workflow:drafts", null, null, PermissionCodes.WorkflowDraftDelete, "", 3, "Button"),
        new("workflow:instance:withdraw", "撤回流程", "workflow:mine", null, null, PermissionCodes.WorkflowInstanceWithdraw, "", 1, "Button"),
        new("workflow:instance:terminate", "终止流程", "workflow:monitoring", null, null, PermissionCodes.WorkflowInstanceTerminate, "", 1, "Button"),
        new("workflow:instance:variable", "流程变量", "workflow:monitoring", null, null, PermissionCodes.WorkflowInstanceVariable, "", 2, "Button"),
        new("workflow:deployment:resource", "流程定义资源", "workflow:models", null, null, PermissionCodes.WorkflowDeploymentResource, "", 6, "Button"),
        new("workflow:delegation:edit", "保存委托", "workflow:delegations", null, null, PermissionCodes.WorkflowDelegationEdit, "", 1, "Button"),
        new("workflow:delegation:delete", "删除委托", "workflow:delegations", null, null, PermissionCodes.WorkflowDelegationDelete, "", 2, "Button"),
        new("workflow:notification:channel", "渠道配置", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationChannelQuery, "", 1, "Button"),
        new("workflow:notification:channel:edit", "保存渠道", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationChannelEdit, "", 2, "Button"),
        new("workflow:notification:channel:delete", "删除渠道", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationChannelDelete, "", 3, "Button"),
        new("workflow:notification:template", "消息模板", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationTemplateQuery, "", 4, "Button"),
        new("workflow:notification:template:edit", "保存模板", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationTemplateEdit, "", 5, "Button"),
        new("workflow:notification:template:delete", "删除模板", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationTemplateDelete, "", 6, "Button"),
        new("workflow:notification:rule", "通知规则", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationRuleQuery, "", 7, "Button"),
        new("workflow:notification:rule:edit", "保存规则", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationRuleEdit, "", 8, "Button"),
        new("workflow:notification:rule:delete", "删除规则", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationRuleDelete, "", 9, "Button"),
        new("workflow:notification:task", "通知任务", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationTaskQuery, "", 10, "Button"),
        new("workflow:notification:task:send", "发送通知任务", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationTaskSend, "", 11, "Button"),
        new("workflow:notification:log", "通知日志", "workflow:notifications", null, null, PermissionCodes.WorkflowNotificationLogQuery, "", 12, "Button"),
        new("workflow:calendar:edit", "保存日历", "workflow:calendars", null, null, PermissionCodes.WorkflowCalendarEdit, "", 1, "Button"),
        new("workflow:calendar:delete", "删除日历", "workflow:calendars", null, null, PermissionCodes.WorkflowCalendarDelete, "", 2, "Button")
    ];
}

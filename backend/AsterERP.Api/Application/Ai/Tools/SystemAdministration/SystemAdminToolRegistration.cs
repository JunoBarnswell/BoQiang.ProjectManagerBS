using AsterERP.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Api.Application.Ai.Tools.SystemAdministration;

public static class SystemAdminToolRegistration
{
    private static readonly IReadOnlyList<string> ReadModes = ["Ask", "Plan", "Agent"];
    private static readonly IReadOnlyList<string> AgentModes = ["Agent"];
    private static readonly IReadOnlyList<string> Id = ["id"];
    private static readonly IReadOnlyList<string> Request = ["request"];
    private static readonly IReadOnlyList<string> IdAndRequest = ["id", "request"];
    private static readonly IReadOnlyList<string> IdsAndStatus = ["ids", "status"];
    private static readonly IReadOnlyList<string> PasswordArguments = ["password"];
    private static readonly IReadOnlyList<string> ParameterSensitiveArguments = ["paramValue", "request"];
    private static readonly IReadOnlyList<string> ScheduledSensitiveArguments = ["headers", "bodyJson", "parameters"];

    public static void RegisterSystemAdministrationTools(IServiceCollection services)
    {
        services.AddScoped<SystemAdminToolHandlers>();

        Add(services, AiSystemAdminToolCodes.UserSearch, "查询用户", "分页查询系统用户", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemUserQuery, ReadModes, [], h => h.SearchUsersAsync);
        Add(services, AiSystemAdminToolCodes.UserGet, "查看用户", "查看系统用户详情", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemUserQuery, ReadModes, Id, h => h.GetUserAsync);
        Add(services, AiSystemAdminToolCodes.UserCreate, "新增用户", "新增系统用户", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemUserAdd, AgentModes, Request, h => h.CreateUserAsync, sensitiveArguments: PasswordArguments);
        Add(services, AiSystemAdminToolCodes.UserUpdate, "编辑用户", "编辑系统用户", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemUserEdit, AgentModes, IdAndRequest, h => h.UpdateUserAsync, sensitiveArguments: PasswordArguments);
        Add(services, AiSystemAdminToolCodes.UserDelete, "删除用户", "删除系统用户", "L4", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemUserDelete, AgentModes, Id, h => h.DeleteUserAsync);
        Add(services, AiSystemAdminToolCodes.UserBatchStatus, "批量启停用户", "批量更新用户状态", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemUserEdit, AgentModes, IdsAndStatus, h => h.BatchUserStatusAsync);
        Add(services, AiSystemAdminToolCodes.UserGrantRoles, "用户授权角色", "给指定用户分配角色", "L4", AiSystemAdminToolDefinition.AiGrantPermission, PermissionCodes.SystemUserGrantRole, AgentModes, ["userId", "roleIds"], h => h.GrantUserRolesAsync);
        Add(services, AiSystemAdminToolCodes.UserResetPassword, "重置用户密码", "重置指定用户密码", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemUserResetPassword, AgentModes, ["userId", "password"], h => h.ResetUserPasswordAsync, sensitiveArguments: PasswordArguments);

        Add(services, AiSystemAdminToolCodes.RoleSearch, "查询角色", "分页查询角色", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemRoleQuery, ReadModes, [], h => h.SearchRolesAsync);
        Add(services, AiSystemAdminToolCodes.RoleGet, "查看角色", "查看角色详情", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemRoleQuery, ReadModes, Id, h => h.GetRoleAsync);
        Add(services, AiSystemAdminToolCodes.RoleCreate, "新增角色", "新增当前工作区角色", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemRoleAdd, AgentModes, Request, h => h.CreateRoleAsync);
        Add(services, AiSystemAdminToolCodes.RoleUpdate, "编辑角色", "编辑当前工作区角色", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemRoleEdit, AgentModes, IdAndRequest, h => h.UpdateRoleAsync);
        Add(services, AiSystemAdminToolCodes.RoleDelete, "删除角色", "删除角色", "L4", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemRoleDelete, AgentModes, Id, h => h.DeleteRoleAsync);
        Add(services, AiSystemAdminToolCodes.RoleBatchStatus, "批量启停角色", "批量更新角色启用状态", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemRoleEdit, AgentModes, IdsAndStatus, h => h.BatchRoleStatusAsync);
        Add(services, AiSystemAdminToolCodes.RoleGrantMenus, "角色授权菜单", "给角色分配菜单权限码", "L4", AiSystemAdminToolDefinition.AiGrantPermission, PermissionCodes.SystemRoleGrant, AgentModes, ["roleId"], h => h.GrantRoleMenusAsync);

        Add(services, AiSystemAdminToolCodes.MenuSearch, "查询菜单", "分页查询菜单", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemMenuQuery, ReadModes, [], h => h.SearchMenusAsync);
        Add(services, AiSystemAdminToolCodes.MenuTree, "菜单树", "查询当前工作区菜单树", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemMenuQuery, ReadModes, [], h => h.GetMenuTreeAsync);
        Add(services, AiSystemAdminToolCodes.MenuGet, "查看菜单", "查看菜单详情", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemMenuQuery, ReadModes, Id, h => h.GetMenuAsync);
        Add(services, AiSystemAdminToolCodes.MenuCreate, "新增菜单", "新增当前工作区菜单", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemMenuAdd, AgentModes, Request, h => h.CreateMenuAsync);
        Add(services, AiSystemAdminToolCodes.MenuUpdate, "编辑菜单", "编辑当前工作区菜单", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemMenuEdit, AgentModes, IdAndRequest, h => h.UpdateMenuAsync);
        Add(services, AiSystemAdminToolCodes.MenuDelete, "删除菜单", "删除菜单", "L4", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemMenuDelete, AgentModes, Id, h => h.DeleteMenuAsync);
        Add(services, AiSystemAdminToolCodes.MenuBatchStatus, "批量显隐菜单", "批量更新菜单状态", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemMenuEdit, AgentModes, IdsAndStatus, h => h.BatchMenuStatusAsync);

        Add(services, AiSystemAdminToolCodes.DepartmentSearch, "查询部门", "分页查询部门", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemDeptQuery, ReadModes, [], h => h.SearchDepartmentsAsync);
        Add(services, AiSystemAdminToolCodes.DepartmentTree, "部门树", "查询部门树", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemDeptQuery, ReadModes, [], h => h.GetDepartmentTreeAsync);
        Add(services, AiSystemAdminToolCodes.DepartmentGet, "查看部门", "查看部门详情", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemDeptQuery, ReadModes, Id, h => h.GetDepartmentAsync);
        Add(services, AiSystemAdminToolCodes.DepartmentCreate, "新增部门", "新增部门", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemDeptAdd, AgentModes, Request, h => h.CreateDepartmentAsync);
        Add(services, AiSystemAdminToolCodes.DepartmentUpdate, "编辑部门", "编辑部门", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemDeptEdit, AgentModes, IdAndRequest, h => h.UpdateDepartmentAsync);
        Add(services, AiSystemAdminToolCodes.DepartmentDelete, "删除部门", "删除部门", "L4", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemDeptDelete, AgentModes, Id, h => h.DeleteDepartmentAsync);
        Add(services, AiSystemAdminToolCodes.DepartmentBatchStatus, "批量启停部门", "批量更新部门状态", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemDeptEdit, AgentModes, IdsAndStatus, h => h.BatchDepartmentStatusAsync);

        Add(services, AiSystemAdminToolCodes.PositionSearch, "查询岗位", "分页查询岗位", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemPositionQuery, ReadModes, [], h => h.SearchPositionsAsync);
        Add(services, AiSystemAdminToolCodes.PositionGet, "查看岗位", "查看岗位详情", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemPositionQuery, ReadModes, Id, h => h.GetPositionAsync);
        Add(services, AiSystemAdminToolCodes.PositionCreate, "新增岗位", "新增岗位", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemPositionAdd, AgentModes, Request, h => h.CreatePositionAsync);
        Add(services, AiSystemAdminToolCodes.PositionUpdate, "编辑岗位", "编辑岗位", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemPositionEdit, AgentModes, IdAndRequest, h => h.UpdatePositionAsync);
        Add(services, AiSystemAdminToolCodes.PositionDelete, "删除岗位", "删除岗位", "L4", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemPositionDelete, AgentModes, Id, h => h.DeletePositionAsync);
        Add(services, AiSystemAdminToolCodes.PositionBatchStatus, "批量启停岗位", "批量更新岗位状态", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemPositionEdit, AgentModes, IdsAndStatus, h => h.BatchPositionStatusAsync);

        Add(services, AiSystemAdminToolCodes.DictTypeSearch, "查询字典类型", "分页查询字典类型", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemDictQuery, ReadModes, [], h => h.SearchDictTypesAsync);
        Add(services, AiSystemAdminToolCodes.DictTypeGet, "查看字典类型", "查看字典类型详情", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemDictQuery, ReadModes, Id, h => h.GetDictTypeAsync);
        Add(services, AiSystemAdminToolCodes.DictTypeCreate, "新增字典类型", "新增字典类型", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemDictAdd, AgentModes, Request, h => h.CreateDictTypeAsync);
        Add(services, AiSystemAdminToolCodes.DictTypeUpdate, "编辑字典类型", "编辑字典类型", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemDictEdit, AgentModes, IdAndRequest, h => h.UpdateDictTypeAsync);
        Add(services, AiSystemAdminToolCodes.DictTypeDelete, "删除字典类型", "删除字典类型", "L4", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemDictDelete, AgentModes, Id, h => h.DeleteDictTypeAsync);
        Add(services, AiSystemAdminToolCodes.DictItemSearch, "查询字典项", "分页查询字典项", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemDictQuery, ReadModes, ["dictTypeId"], h => h.SearchDictItemsAsync);
        Add(services, AiSystemAdminToolCodes.DictItemGet, "查看字典项", "查看字典项详情", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemDictQuery, ReadModes, Id, h => h.GetDictItemAsync);
        Add(services, AiSystemAdminToolCodes.DictItemCreate, "新增字典项", "新增字典项", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemDictAdd, AgentModes, ["dictTypeId", "request"], h => h.CreateDictItemAsync);
        Add(services, AiSystemAdminToolCodes.DictItemUpdate, "编辑字典项", "编辑字典项", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemDictEdit, AgentModes, IdAndRequest, h => h.UpdateDictItemAsync);
        Add(services, AiSystemAdminToolCodes.DictItemDelete, "删除字典项", "删除字典项", "L4", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemDictDelete, AgentModes, Id, h => h.DeleteDictItemAsync);

        Add(services, AiSystemAdminToolCodes.ParameterSearch, "查询系统参数", "分页查询系统参数", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemParameterQuery, ReadModes, [], h => h.SearchParametersAsync);
        Add(services, AiSystemAdminToolCodes.ParameterCreate, "新增系统参数", "新增系统参数", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemParameterAdd, AgentModes, Request, h => h.CreateParameterAsync, sensitiveArguments: ParameterSensitiveArguments);
        Add(services, AiSystemAdminToolCodes.ParameterUpdate, "编辑系统参数", "编辑系统参数", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemParameterEdit, AgentModes, IdAndRequest, h => h.UpdateParameterAsync, sensitiveArguments: ParameterSensitiveArguments);
        Add(services, AiSystemAdminToolCodes.ParameterDelete, "删除系统参数", "删除系统参数", "L4", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemParameterDelete, AgentModes, Id, h => h.DeleteParameterAsync);
        Add(services, AiSystemAdminToolCodes.ParameterBatchStatus, "批量启停系统参数", "批量更新系统参数状态", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemParameterEdit, AgentModes, IdsAndStatus, h => h.BatchParameterStatusAsync);

        Add(services, AiSystemAdminToolCodes.AnnouncementSearch, "查询通知公告", "分页查询通知公告", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemAnnouncementQuery, ReadModes, [], h => h.SearchAnnouncementsAsync);
        Add(services, AiSystemAdminToolCodes.AnnouncementCreate, "新增通知公告", "新增通知公告", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemAnnouncementAdd, AgentModes, Request, h => h.CreateAnnouncementAsync);
        Add(services, AiSystemAdminToolCodes.AnnouncementUpdate, "编辑通知公告", "编辑通知公告", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemAnnouncementEdit, AgentModes, IdAndRequest, h => h.UpdateAnnouncementAsync);
        Add(services, AiSystemAdminToolCodes.AnnouncementDelete, "删除通知公告", "删除通知公告", "L4", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemAnnouncementDelete, AgentModes, Id, h => h.DeleteAnnouncementAsync);
        Add(services, AiSystemAdminToolCodes.AnnouncementPublish, "发布通知公告", "发布通知公告", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemAnnouncementPublish, AgentModes, Id, h => h.PublishAnnouncementAsync);
        Add(services, AiSystemAdminToolCodes.AnnouncementWithdraw, "撤回通知公告", "撤回通知公告", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemAnnouncementWithdraw, AgentModes, Id, h => h.WithdrawAnnouncementAsync);
        Add(services, AiSystemAdminToolCodes.AnnouncementTop, "置顶通知公告", "置顶或取消置顶通知公告", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemAnnouncementTop, AgentModes, Id, h => h.TopAnnouncementAsync);

        Add(services, AiSystemAdminToolCodes.OperationLogSearch, "查询操作日志", "分页查询操作日志", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemOperationLogQuery, ReadModes, [], h => h.SearchOperationLogsAsync);
        Add(services, AiSystemAdminToolCodes.OperationLogGet, "查看操作日志", "查看操作日志详情", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemOperationLogQuery, ReadModes, Id, h => h.GetOperationLogAsync);
        Add(services, AiSystemAdminToolCodes.OperationLogRecent, "最近操作日志", "查询最近操作日志", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemOperationLogQuery, ReadModes, [], h => h.RecentOperationLogsAsync);
        Add(services, AiSystemAdminToolCodes.LoginLogSearch, "查询登录日志", "分页查询登录日志", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemLoginLogQuery, ReadModes, [], h => h.SearchLoginLogsAsync);
        Add(services, AiSystemAdminToolCodes.OnlineUserSearch, "查询在线用户", "分页查询在线用户", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemOnlineUserQuery, ReadModes, [], h => h.SearchOnlineUsersAsync);
        Add(services, AiSystemAdminToolCodes.OnlineUserForceLogout, "强退在线用户", "强制退出指定在线会话", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemOnlineUserKick, AgentModes, ["sessionId"], h => h.ForceLogoutOnlineUserAsync);

        Add(services, AiSystemAdminToolCodes.ScheduledJobSearch, "查询任务调度", "分页查询任务调度", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemScheduledJobQuery, ReadModes, [], h => h.SearchScheduledJobsAsync);
        Add(services, AiSystemAdminToolCodes.ScheduledJobGet, "查看任务调度", "查看任务调度详情", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemScheduledJobQuery, ReadModes, Id, h => h.GetScheduledJobAsync);
        Add(services, AiSystemAdminToolCodes.ScheduledJobCreate, "新增任务调度", "新增任务调度", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemScheduledJobAdd, AgentModes, Request, h => h.CreateScheduledJobAsync, sensitiveArguments: ScheduledSensitiveArguments);
        Add(services, AiSystemAdminToolCodes.ScheduledJobUpdate, "编辑任务调度", "编辑任务调度", "L3", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemScheduledJobEdit, AgentModes, IdAndRequest, h => h.UpdateScheduledJobAsync, sensitiveArguments: ScheduledSensitiveArguments);
        Add(services, AiSystemAdminToolCodes.ScheduledJobDelete, "删除任务调度", "删除任务调度", "L4", AiSystemAdminToolDefinition.AiWritePermission, PermissionCodes.SystemScheduledJobDelete, AgentModes, Id, h => h.DeleteScheduledJobAsync);
        Add(services, AiSystemAdminToolCodes.ScheduledJobPause, "暂停任务调度", "暂停任务调度", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemScheduledJobEdit, AgentModes, Id, h => h.PauseScheduledJobAsync);
        Add(services, AiSystemAdminToolCodes.ScheduledJobResume, "恢复任务调度", "恢复任务调度", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemScheduledJobEdit, AgentModes, Id, h => h.ResumeScheduledJobAsync);
        Add(services, AiSystemAdminToolCodes.ScheduledJobTrigger, "触发任务调度", "手动触发任务调度", "L4", AiSystemAdminToolDefinition.AiOperatePermission, PermissionCodes.SystemScheduledJobTrigger, AgentModes, Id, h => h.TriggerScheduledJobAsync);
        Add(services, AiSystemAdminToolCodes.ScheduledJobLogs, "查询任务日志", "分页查询任务调度日志", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemScheduledJobLog, ReadModes, Id, h => h.GetScheduledJobLogsAsync);
        Add(services, AiSystemAdminToolCodes.ScheduledJobSummary, "任务调度汇总", "查询任务调度汇总", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemScheduledJobQuery, ReadModes, [], h => h.GetScheduledJobSummaryAsync);
        Add(services, AiSystemAdminToolCodes.ScheduledJobTypes, "任务调度类型", "查询任务调度可选类型", "L1", AiSystemAdminToolDefinition.AiReadPermission, PermissionCodes.SystemScheduledJobQuery, ReadModes, [], h => h.GetScheduledJobTypesAsync);
    }

    private static void Add(
        IServiceCollection services,
        string toolCode,
        string toolName,
        string description,
        string riskLevel,
        string aiPermissionCode,
        string systemPermissionCode,
        IReadOnlyList<string> workModes,
        IReadOnlyList<string> requiredArguments,
        Func<SystemAdminToolHandlers, Func<AiKernelFunctionContext, CancellationToken, Task<AiKernelFunctionResult>>> handler,
        IReadOnlyList<string>? sensitiveArguments = null)
    {
        services.AddScoped<IAiKernelFunction>(serviceProvider =>
        {
            var handlers = serviceProvider.GetRequiredService<SystemAdminToolHandlers>();
            return new SystemAdminRegisteredTool(
                AiSystemAdminToolDefinition.Create(
                    toolCode,
                    toolName,
                    description,
                    riskLevel,
                    aiPermissionCode,
                    systemPermissionCode,
                    workModes,
                    requiredArguments,
                    sensitiveArguments),
                handler(handlers));
        });
    }
}

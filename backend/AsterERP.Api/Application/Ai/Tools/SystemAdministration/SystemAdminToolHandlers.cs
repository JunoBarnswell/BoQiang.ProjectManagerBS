using AsterERP.Api.Application.System.Announcements;
using AsterERP.Api.Application.System.Dicts;
using AsterERP.Api.Application.System.LoginLogs;
using AsterERP.Api.Application.System.Menus;
using AsterERP.Api.Application.System.OnlineUsers;
using AsterERP.Api.Application.System.Organizations;
using AsterERP.Api.Application.System.Parameters;
using AsterERP.Api.Application.System.Roles;
using AsterERP.Api.Application.System.ScheduledJobs;
using AsterERP.Api.Application.System.Users;
using AsterERP.Api.Infrastructure.Logging;
using AsterERP.Contracts.Logs;
using AsterERP.Contracts.System.Announcements;
using AsterERP.Contracts.System.Dicts;
using AsterERP.Contracts.System.Menus;
using AsterERP.Contracts.System.OnlineUsers;
using AsterERP.Contracts.System.Organizations;
using AsterERP.Contracts.System.Parameters;
using AsterERP.Contracts.System.Roles;
using AsterERP.Contracts.System.ScheduledJobs;
using AsterERP.Contracts.System.Users;
using AsterERP.Shared;
using R = AsterERP.Api.Application.Ai.Tools.SystemAdministration.AiSystemAdminArgumentReader;

namespace AsterERP.Api.Application.Ai.Tools.SystemAdministration;

public sealed class SystemAdminToolHandlers(
    ISystemUserService userService,
    ISystemRoleService roleService,
    ISystemMenuService menuService,
    ISystemDepartmentService departmentService,
    ISystemPositionService positionService,
    IDictManagementService dictService,
    IParameterService parameterService,
    IAnnouncementService announcementService,
    IOperationLogService operationLogService,
    ILoginLogService loginLogService,
    IOnlineUserService onlineUserService,
    IScheduledJobService scheduledJobService)
{
    public async Task<AiKernelFunctionResult> SearchUsersAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Page(context, "user", "search", await userService.GetPageAsync(ReadGrid(context), cancellationToken));

    public async Task<AiKernelFunctionResult> GetUserAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "user", "get", await userService.GetDetailAsync(ReadId(context), cancellationToken));

    public async Task<AiKernelFunctionResult> CreateUserAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var request = ReadRequest<UserUpsertRequest>(context);
        var item = await userService.CreateAsync(request, cancellationToken);
        return Item(context, "user", "create", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> UpdateUserAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var item = await userService.UpdateAsync(id, ReadRequest<UserUpsertRequest>(context), cancellationToken);
        return Item(context, "user", "update", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> DeleteUserAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await userService.DeleteAsync(id, cancellationToken);
        return Done(context, "user", "delete", [id]);
    }

    public async Task<AiKernelFunctionResult> BatchUserStatusAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var ids = ReadIds(context);
        await userService.BatchUpdateStatusAsync(ids, ReadStatus(context), cancellationToken);
        return Done(context, "user", "batchStatus", ids);
    }

    public async Task<AiKernelFunctionResult> GrantUserRolesAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var userId = ReadId(context, "userId");
        var roleIds = R.ReadStringList(context, "roleIds");
        R.EnsureNonEmpty(roleIds, "roleIds");
        await userService.UpdateRolesAsync(userId, new UserRoleUpdateRequest(roleIds), cancellationToken);
        return Done(context, "user", "grantRoles", [userId]);
    }

    public async Task<AiKernelFunctionResult> ResetUserPasswordAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var userId = ReadId(context, "userId");
        var password = R.ReadRequiredString(context, "password");
        await userService.ResetPasswordAsync(userId, new UserResetPasswordRequest(password), cancellationToken);
        return Done(context, "user", "resetPassword", [userId]);
    }

    public async Task<AiKernelFunctionResult> SearchRolesAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Page(context, "role", "search", await roleService.GetPageAsync(ReadGrid(context), cancellationToken));

    public async Task<AiKernelFunctionResult> GetRoleAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "role", "get", await roleService.GetDetailAsync(ReadId(context), cancellationToken));

    public async Task<AiKernelFunctionResult> CreateRoleAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var item = await roleService.CreateAsync(WithWorkspace(context, ReadRequest<RoleUpsertRequest>(context)), cancellationToken);
        return Item(context, "role", "create", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> UpdateRoleAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var item = await roleService.UpdateAsync(id, WithWorkspace(context, ReadRequest<RoleUpsertRequest>(context)), cancellationToken);
        return Item(context, "role", "update", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> DeleteRoleAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await roleService.DeleteAsync(id, cancellationToken);
        return Done(context, "role", "delete", [id]);
    }

    public async Task<AiKernelFunctionResult> BatchRoleStatusAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var ids = ReadIds(context);
        await roleService.BatchUpdateStatusAsync(ids, ReadStatus(context), cancellationToken);
        return Done(context, "role", "batchStatus", ids);
    }

    public async Task<AiKernelFunctionResult> GrantRoleMenusAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var roleId = ReadId(context, "roleId");
        var permissionCodes = R.ReadStringList(context, "permissionCodes").ToList();
        var warnings = new List<string>();
        var menuCodes = R.ReadStringList(context, "menuCodes");
        if (menuCodes.Count > 0)
        {
            var tree = await roleService.GetPermissionTreeAsync(ReadGrid(context), cancellationToken);
            var flatMenus = FlattenMenus(tree).ToList();
            var menuCodeSet = new HashSet<string>(menuCodes, StringComparer.OrdinalIgnoreCase);
            var resolved = flatMenus
                .Where(item => menuCodeSet.Contains(item.MenuCode) && !string.IsNullOrWhiteSpace(item.PermissionCode))
                .Select(item => item.PermissionCode!)
                .ToList();
            permissionCodes.AddRange(resolved);
            foreach (var menuCode in menuCodeSet)
            {
                var match = flatMenus.FirstOrDefault(item => string.Equals(item.MenuCode, menuCode, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    warnings.Add($"菜单编码不存在或不属于当前工作区：{menuCode}");
                }
                else if (string.IsNullOrWhiteSpace(match.PermissionCode))
                {
                    warnings.Add($"菜单未配置权限码，已跳过：{menuCode}");
                }
            }
        }

        var finalCodes = permissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        R.EnsureNonEmpty(finalCodes, "permissionCodes/menuCodes");
        await roleService.UpdatePermissionsAsync(roleId, new RolePermissionUpdateRequest(finalCodes), cancellationToken);
        return AiSystemAdminToolResult.Succeeded(context, "role", "grantMenus", item: new { permissionCodes = finalCodes }, affectedIds: [roleId], warnings: warnings);
    }

    public async Task<AiKernelFunctionResult> SearchMenusAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Page(context, "menu", "search", await menuService.GetPageAsync(ReadGrid(context), cancellationToken));

    public async Task<AiKernelFunctionResult> GetMenuTreeAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "menu", "tree", await menuService.GetTreeAsync(ReadGrid(context), cancellationToken));

    public async Task<AiKernelFunctionResult> GetMenuAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "menu", "get", await menuService.GetDetailAsync(ReadId(context), cancellationToken));

    public async Task<AiKernelFunctionResult> CreateMenuAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var item = await menuService.CreateAsync(WithWorkspace(context, ReadRequest<MenuUpsertRequest>(context)), cancellationToken);
        return Item(context, "menu", "create", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> UpdateMenuAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var item = await menuService.UpdateAsync(id, WithWorkspace(context, ReadRequest<MenuUpsertRequest>(context)), cancellationToken);
        return Item(context, "menu", "update", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> DeleteMenuAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await menuService.DeleteAsync(id, cancellationToken);
        return Done(context, "menu", "delete", [id]);
    }

    public async Task<AiKernelFunctionResult> BatchMenuStatusAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var ids = ReadIds(context);
        await menuService.BatchUpdateStatusAsync(ids, ReadStatus(context), cancellationToken);
        return Done(context, "menu", "batchStatus", ids);
    }

    public async Task<AiKernelFunctionResult> SearchDepartmentsAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Page(context, "department", "search", await departmentService.GetPageAsync(ReadGrid(context), cancellationToken));

    public async Task<AiKernelFunctionResult> GetDepartmentTreeAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "department", "tree", await departmentService.GetTreeAsync(cancellationToken));

    public async Task<AiKernelFunctionResult> GetDepartmentAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "department", "get", await departmentService.GetDetailAsync(ReadId(context), cancellationToken));

    public async Task<AiKernelFunctionResult> CreateDepartmentAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var item = await departmentService.CreateAsync(ReadRequest<DepartmentUpsertRequest>(context), cancellationToken);
        return Item(context, "department", "create", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> UpdateDepartmentAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var item = await departmentService.UpdateAsync(id, ReadRequest<DepartmentUpsertRequest>(context), cancellationToken);
        return Item(context, "department", "update", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> DeleteDepartmentAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await departmentService.DeleteAsync(id, cancellationToken);
        return Done(context, "department", "delete", [id]);
    }

    public async Task<AiKernelFunctionResult> BatchDepartmentStatusAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var ids = ReadIds(context);
        await departmentService.BatchUpdateStatusAsync(ids, ReadStatus(context), cancellationToken);
        return Done(context, "department", "batchStatus", ids);
    }

    public async Task<AiKernelFunctionResult> SearchPositionsAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Page(context, "position", "search", await positionService.GetPageAsync(ReadGrid(context), cancellationToken));

    public async Task<AiKernelFunctionResult> GetPositionAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "position", "get", await positionService.GetDetailAsync(ReadId(context), cancellationToken));

    public async Task<AiKernelFunctionResult> CreatePositionAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var item = await positionService.CreateAsync(ReadRequest<PositionUpsertRequest>(context), cancellationToken);
        return Item(context, "position", "create", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> UpdatePositionAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var item = await positionService.UpdateAsync(id, ReadRequest<PositionUpsertRequest>(context), cancellationToken);
        return Item(context, "position", "update", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> DeletePositionAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await positionService.DeleteAsync(id, cancellationToken);
        return Done(context, "position", "delete", [id]);
    }

    public async Task<AiKernelFunctionResult> BatchPositionStatusAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var ids = ReadIds(context);
        await positionService.BatchUpdateStatusAsync(ids, ReadStatus(context), cancellationToken);
        return Done(context, "position", "batchStatus", ids);
    }

    public async Task<AiKernelFunctionResult> SearchDictTypesAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Page(context, "dict.type", "search", await dictService.GetTypesPageAsync(ReadGrid(context), cancellationToken));

    public async Task<AiKernelFunctionResult> GetDictTypeAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "dict.type", "get", await dictService.GetTypeDetailAsync(ReadId(context), cancellationToken));

    public async Task<AiKernelFunctionResult> CreateDictTypeAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var item = await dictService.CreateTypeAsync(ReadRequest<DictTypeUpsertRequest>(context), cancellationToken);
        return Item(context, "dict.type", "create", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> UpdateDictTypeAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var item = await dictService.UpdateTypeAsync(id, ReadRequest<DictTypeUpsertRequest>(context), cancellationToken);
        return Item(context, "dict.type", "update", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> DeleteDictTypeAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await dictService.DeleteTypeAsync(id, cancellationToken);
        return Done(context, "dict.type", "delete", [id]);
    }

    public async Task<AiKernelFunctionResult> SearchDictItemsAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var query = ReadGrid(context);
        query.ParentId = R.ReadRequiredString(context, "dictTypeId");
        return Page(context, "dict.item", "search", await dictService.GetItemsPageAsync(query, cancellationToken));
    }

    public async Task<AiKernelFunctionResult> GetDictItemAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "dict.item", "get", await dictService.GetItemDetailAsync(ReadId(context), cancellationToken));

    public async Task<AiKernelFunctionResult> CreateDictItemAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dictTypeId = R.ReadRequiredString(context, "dictTypeId");
        var item = await dictService.CreateItemAsync(dictTypeId, ReadRequest<DictItemUpsertRequest>(context), cancellationToken);
        return Item(context, "dict.item", "create", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> UpdateDictItemAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var item = await dictService.UpdateItemAsync(id, ReadRequest<DictItemUpsertRequest>(context), cancellationToken);
        return Item(context, "dict.item", "update", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> DeleteDictItemAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await dictService.DeleteItemAsync(id, cancellationToken);
        return Done(context, "dict.item", "delete", [id]);
    }

    public async Task<AiKernelFunctionResult> SearchParametersAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Page(context, "parameter", "search", await parameterService.GetPageAsync(ReadGrid(context), R.ReadOptionalString(context, "category"), cancellationToken));

    public async Task<AiKernelFunctionResult> CreateParameterAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var item = await parameterService.CreateAsync(ReadRequest<ParameterUpsertRequest>(context), cancellationToken);
        return Item(context, "parameter", "create", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> UpdateParameterAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var item = await parameterService.UpdateAsync(id, ReadRequest<ParameterUpsertRequest>(context), cancellationToken);
        return Item(context, "parameter", "update", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> DeleteParameterAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await parameterService.DeleteAsync(id, cancellationToken);
        return Done(context, "parameter", "delete", [id]);
    }

    public async Task<AiKernelFunctionResult> BatchParameterStatusAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var ids = ReadIds(context);
        await parameterService.BatchUpdateStatusAsync(ids, ReadStatus(context), cancellationToken);
        return Done(context, "parameter", "batchStatus", ids);
    }

    public async Task<AiKernelFunctionResult> SearchAnnouncementsAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Page(context, "announcement", "search", await announcementService.GetPageAsync(ReadGrid(context), cancellationToken));

    public async Task<AiKernelFunctionResult> CreateAnnouncementAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var item = await announcementService.CreateAsync(ReadRequest<AnnouncementUpsertRequest>(context), cancellationToken);
        return Item(context, "announcement", "create", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> UpdateAnnouncementAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var item = await announcementService.UpdateAsync(id, ReadRequest<AnnouncementUpsertRequest>(context), cancellationToken);
        return Item(context, "announcement", "update", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> DeleteAnnouncementAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await announcementService.DeleteAsync(id, cancellationToken);
        return Done(context, "announcement", "delete", [id]);
    }

    public async Task<AiKernelFunctionResult> PublishAnnouncementAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var item = await announcementService.PublishAsync(ReadId(context), cancellationToken);
        return Item(context, "announcement", "publish", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> WithdrawAnnouncementAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var item = await announcementService.WithdrawAsync(ReadId(context), cancellationToken);
        return Item(context, "announcement", "withdraw", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> TopAnnouncementAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var request = R.ReadBool(context, "isTop") is { } isTop
            ? new AnnouncementTopRequest(isTop)
            : ReadRequest<AnnouncementTopRequest>(context);
        var item = await announcementService.SetTopAsync(id, request, cancellationToken);
        return Item(context, "announcement", "top", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> SearchOperationLogsAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var query = R.ClampPagedQuery(ReadRequest<OperationLogQueryRequest>(context, "query"));
        return Page(context, "operationLog", "search", await operationLogService.GetPageAsync(query, cancellationToken));
    }

    public async Task<AiKernelFunctionResult> GetOperationLogAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "operationLog", "get", await operationLogService.GetDetailAsync(ReadId(context), cancellationToken));

    public async Task<AiKernelFunctionResult> RecentOperationLogsAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(R.ReadInt(context, "take") ?? 20, 1, 100);
        var items = await operationLogService.RecentAsync(take, cancellationToken);
        return Item(context, "operationLog", "recent", items);
    }

    public async Task<AiKernelFunctionResult> SearchLoginLogsAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var query = R.ClampPagedQuery(ReadRequest<LoginLogQuery>(context, "query"));
        return Page(context, "loginLog", "search", await loginLogService.GetPageAsync(query, cancellationToken));
    }

    public async Task<AiKernelFunctionResult> SearchOnlineUsersAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var query = R.ClampPagedQuery(ReadRequest<OnlineUserQuery>(context, "query"));
        return Page(context, "onlineUser", "search", await onlineUserService.GetPageAsync(query, cancellationToken));
    }

    public async Task<AiKernelFunctionResult> ForceLogoutOnlineUserAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var sessionId = R.ReadRequiredString(context, "sessionId");
        await onlineUserService.ForceLogoutAsync(sessionId, cancellationToken);
        return Done(context, "onlineUser", "forceLogout", [sessionId]);
    }

    public async Task<AiKernelFunctionResult> SearchScheduledJobsAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Page(context, "scheduledJob", "search", await scheduledJobService.GetPageAsync(ReadGrid(context), R.ReadOptionalString(context, "jobType"), R.ReadOptionalString(context, "result"), cancellationToken));

    public async Task<AiKernelFunctionResult> GetScheduledJobAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "scheduledJob", "get", await scheduledJobService.GetDetailAsync(ReadId(context), cancellationToken));

    public async Task<AiKernelFunctionResult> CreateScheduledJobAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var item = await scheduledJobService.CreateAsync(ReadRequest<ScheduledJobUpsertRequest>(context), cancellationToken);
        return Item(context, "scheduledJob", "create", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> UpdateScheduledJobAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var item = await scheduledJobService.UpdateAsync(id, ReadRequest<ScheduledJobUpsertRequest>(context), cancellationToken);
        return Item(context, "scheduledJob", "update", item, [item.Id]);
    }

    public async Task<AiKernelFunctionResult> DeleteScheduledJobAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await scheduledJobService.DeleteAsync(id, cancellationToken);
        return Done(context, "scheduledJob", "delete", [id]);
    }

    public async Task<AiKernelFunctionResult> PauseScheduledJobAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await scheduledJobService.PauseAsync(id, cancellationToken);
        return Done(context, "scheduledJob", "pause", [id]);
    }

    public async Task<AiKernelFunctionResult> ResumeScheduledJobAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        await scheduledJobService.ResumeAsync(id, cancellationToken);
        return Done(context, "scheduledJob", "resume", [id]);
    }

    public async Task<AiKernelFunctionResult> TriggerScheduledJobAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        var result = await scheduledJobService.TriggerAsync(id, cancellationToken);
        return AiSystemAdminToolResult.Succeeded(context, "scheduledJob", "trigger", item: new { result }, affectedIds: [id]);
    }

    public async Task<AiKernelFunctionResult> GetScheduledJobLogsAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var id = ReadId(context);
        return Page(context, "scheduledJob", "logs", await scheduledJobService.GetLogsAsync(id, ReadGrid(context), R.ReadOptionalString(context, "result"), cancellationToken));
    }

    public async Task<AiKernelFunctionResult> GetScheduledJobSummaryAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "scheduledJob", "summary", await scheduledJobService.GetSummaryAsync(cancellationToken));

    public async Task<AiKernelFunctionResult> GetScheduledJobTypesAsync(AiKernelFunctionContext context, CancellationToken cancellationToken) =>
        Item(context, "scheduledJob", "types", await scheduledJobService.GetTypesAsync(cancellationToken));

    private static GridQuery ReadGrid(AiKernelFunctionContext context) => R.ReadGridQuery(context);

    private static T ReadRequest<T>(AiKernelFunctionContext context, string propertyName = "request") =>
        R.ReadDto<T>(context, propertyName);

    private static string ReadId(AiKernelFunctionContext context, string propertyName = "id") =>
        R.ReadRequiredString(context, propertyName);

    private static string ReadStatus(AiKernelFunctionContext context) =>
        R.ReadRequiredString(context, "status");

    private static IReadOnlyList<string> ReadIds(AiKernelFunctionContext context)
    {
        var ids = R.ReadIds(context);
        R.EnsureNonEmpty(ids, "ids");
        return ids;
    }

    private static RoleUpsertRequest WithWorkspace(AiKernelFunctionContext context, RoleUpsertRequest request) =>
        request with { TenantId = context.TenantId, AppCode = context.AppCode };

    private static MenuUpsertRequest WithWorkspace(AiKernelFunctionContext context, MenuUpsertRequest request) =>
        request with { TenantId = context.TenantId, AppCode = context.AppCode };

    private static AiKernelFunctionResult Page(AiKernelFunctionContext context, string module, string action, object page) =>
        AiSystemAdminToolResult.Succeeded(context, module, action, page: page);

    private static AiKernelFunctionResult Item(AiKernelFunctionContext context, string module, string action, object? item, IReadOnlyList<string>? affectedIds = null) =>
        AiSystemAdminToolResult.Succeeded(context, module, action, item: item, affectedIds: affectedIds);

    private static AiKernelFunctionResult Done(AiKernelFunctionContext context, string module, string action, IReadOnlyList<string> affectedIds) =>
        AiSystemAdminToolResult.Succeeded(context, module, action, affectedIds: affectedIds);

    private static IEnumerable<MenuTreeNodeResponse> FlattenMenus(IEnumerable<MenuTreeNodeResponse> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenMenus(node.Children))
            {
                yield return child;
            }
        }
    }

}

namespace AsterERP.Api.Infrastructure.SignalR;

using AsterERP.Api.Application.Im;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using Microsoft.AspNetCore.SignalR;
using SqlSugar;

public sealed class SystemNotificationHub(
    IImPresenceService presenceService,
    IWorkspaceDatabaseAccessor databaseAccessor,
    IProjectManagementRealtimeSubscriptionRegistry projectManagementSubscriptions) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst(AsterErpClaimTypes.TenantId)?.Value;
        var appCode = Context.User?.FindFirst(AsterErpClaimTypes.AppCode)?.Value;
        var userId = Context.User?.FindFirst(AsterErpClaimTypes.UserId)?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildImUserGroupName(tenantId, userId));
        }

        if (!string.IsNullOrWhiteSpace(tenantId) &&
            !string.IsNullOrWhiteSpace(appCode) &&
            !string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildImPresenceGroupName(tenantId, appCode));
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildProjectManagementNotificationUserGroupName(tenantId, appCode, userId));
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildProjectManagementOperationUserGroupName(tenantId, appCode, userId));
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildProjectManagementHomeGroupName(tenantId, appCode, userId));
            var changed = await presenceService.ConnectedAsync(tenantId, appCode, userId, Context.ConnectionAborted);
            if (changed is not null)
            {
                await Clients.Group(BuildImPresenceGroupName(tenantId, appCode))
                    .SendAsync("ImPresenceChanged", changed, Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        projectManagementSubscriptions.UnregisterConnection(Context.ConnectionId);
        var tenantId = Context.User?.FindFirst(AsterErpClaimTypes.TenantId)?.Value;
        var appCode = Context.User?.FindFirst(AsterErpClaimTypes.AppCode)?.Value;
        var userId = Context.User?.FindFirst(AsterErpClaimTypes.UserId)?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId) &&
            !string.IsNullOrWhiteSpace(appCode) &&
            !string.IsNullOrWhiteSpace(userId))
        {
            var changed = await presenceService.DisconnectedAsync(tenantId, appCode, userId);
            if (changed is not null)
            {
                await Clients.Group(BuildImPresenceGroupName(tenantId, appCode))
                    .SendAsync("ImPresenceChanged", changed);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinProjectManagementProject(string projectId)
    {
        var tenantId = Context.User?.FindFirst(AsterErpClaimTypes.TenantId)?.Value;
        var appCode = Context.User?.FindFirst(AsterErpClaimTypes.AppCode)?.Value;
        var userId = Context.User?.FindFirst(AsterErpClaimTypes.UserId)?.Value;
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(projectId)) throw new HubException("项目管理实时订阅上下文无效");
        var canRead = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>().Where(project => project.Id == projectId && project.TenantId == tenantId && project.AppCode == appCode && !project.IsDeleted && (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>().Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == appCode && member.UserId == userId && member.IsActive && !member.IsDeleted).Any())).AnyAsync(Context.ConnectionAborted);
        if (!canRead) throw new HubException("无权订阅该项目");
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildProjectManagementProjectGroupName(tenantId, appCode, projectId), Context.ConnectionAborted);
        projectManagementSubscriptions.Register(Context.ConnectionId, tenantId, appCode, projectId, userId);
    }

    public Task LeaveProjectManagementProject(string projectId)
    {
        var tenantId = Context.User?.FindFirst(AsterErpClaimTypes.TenantId)?.Value;
        var appCode = Context.User?.FindFirst(AsterErpClaimTypes.AppCode)?.Value;
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode) || string.IsNullOrWhiteSpace(projectId)) return Task.CompletedTask;
        projectManagementSubscriptions.Unregister(Context.ConnectionId, tenantId, appCode, projectId);
        return
            Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildProjectManagementProjectGroupName(tenantId, appCode, projectId), Context.ConnectionAborted);
    }

    public static string BuildImUserGroupName(string tenantId, string userId) =>
        $"im:user:{tenantId.Trim()}:{userId.Trim()}";

    public static string BuildImPresenceGroupName(string tenantId, string appCode) =>
        $"im:presence:{tenantId.Trim()}:{appCode.Trim().ToUpperInvariant()}";

    public static string BuildProjectManagementProjectGroupName(string tenantId, string appCode, string projectId) =>
        $"pm:project:{tenantId.Trim()}:{appCode.Trim().ToUpperInvariant()}:{projectId.Trim()}";

    public static string BuildProjectManagementNotificationUserGroupName(string tenantId, string appCode, string userId) =>
        $"pm:notification:{tenantId.Trim()}:{appCode.Trim().ToUpperInvariant()}:{userId.Trim()}";

    public static string BuildProjectManagementOperationUserGroupName(string tenantId, string appCode, string userId) =>
        $"pm:operation:{tenantId.Trim()}:{appCode.Trim().ToUpperInvariant()}:{userId.Trim()}";

    public static string BuildProjectManagementHomeGroupName(string tenantId, string appCode, string userId) =>
        $"pm:home:{tenantId.Trim()}:{appCode.Trim().ToUpperInvariant()}:{userId.Trim()}";
}

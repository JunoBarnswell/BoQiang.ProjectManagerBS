using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementNotificationService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementRealtimeTransport? realtimeTransport = null) : IProjectManagementNotificationService
{
    public async Task PublishAsync(ProjectManagementNotification notification, CancellationToken cancellationToken = default)
    {
        EnsureWorkspace(notification);
        var recipient = Required(notification.RecipientUserId);
        var targetRoute = Required(notification.TargetRoute);
        var key = BuildIdempotencyKey(Required(notification.NotificationType), recipient, targetRoute, Required(notification.TraceId));
        var db = databaseAccessor.GetCurrentDb();
        if (await db.Queryable<ProjectManagementNotificationEntity>().AnyAsync(item => item.IdempotencyKey == key && !item.IsDeleted, cancellationToken)) return;
        try
        {
            var entity = new ProjectManagementNotificationEntity
            {
                TenantId = Tenant(), AppCode = App(), RecipientUserId = recipient, NotificationType = Required(notification.NotificationType),
                Title = Required(notification.Title), Message = Required(notification.Message), TargetRoute = targetRoute,
                ProjectId = Optional(notification.ProjectId), TaskId = Optional(notification.TaskId), TraceId = Required(notification.TraceId),
                IdempotencyKey = key, CreatedBy = User(), CreatedTime = DateTime.UtcNow
            };
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            if (realtimeTransport is not null)
            {
                try
                {
                    await realtimeTransport.PublishNotificationCreatedAsync(Tenant(), App(), recipient, entity.Id, cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // The persisted notification is the durable fallback when a browser is offline or SignalR is unavailable.
                }
            }
        }
        catch (Exception exception) when (IsDuplicateNotification(exception))
        {
            return;
        }
    }

    public async Task<ProjectManagementNotificationPageResponse> QueryAsync(ProjectManagementNotificationQuery query, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var notificationType = Optional(query.NotificationType);
        var tenantId = Tenant();
        var appCode = App();
        var userId = User();
        var db = databaseAccessor.GetCurrentDb();
        var itemsQuery = db.Queryable<ProjectManagementNotificationEntity>().Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.RecipientUserId == userId && !item.IsDeleted);
        if (query.UnreadOnly) itemsQuery = itemsQuery.Where(item => !item.IsRead);
        if (!string.IsNullOrWhiteSpace(notificationType)) itemsQuery = itemsQuery.Where(item => item.NotificationType == notificationType);
        var total = await itemsQuery.CountAsync(cancellationToken);
        var unreadCount = await db.Queryable<ProjectManagementNotificationEntity>().Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.RecipientUserId == userId && !item.IsRead && !item.IsDeleted).CountAsync(cancellationToken);
        var items = await itemsQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new ProjectManagementNotificationPageResponse(total, unreadCount, items.Select(Map).ToList());
    }

    public async Task MarkReadAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        if (entity.IsRead) return;
        entity.IsRead = true; entity.ReadTime = DateTime.UtcNow; entity.UpdatedBy = User(); entity.UpdatedTime = entity.ReadTime;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tenantId = Tenant();
        var appCode = App();
        var userId = User();
        await databaseAccessor.GetCurrentDb().Updateable<ProjectManagementNotificationEntity>()
            .SetColumns(item => new ProjectManagementNotificationEntity { IsRead = true, ReadTime = now, UpdatedBy = userId, UpdatedTime = now })
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.RecipientUserId == userId && !item.IsRead && !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ProjectManagementNotificationOpenResponse> OpenAsync(string id, CancellationToken cancellationToken = default)
    {
        var notification = await GetOwnedAsync(id, cancellationToken);
        await MarkReadAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(notification.ProjectId)) return new ProjectManagementNotificationOpenResponse(false, null, "通知未关联可打开的项目对象");
        try
        {
            await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanViewProjectAsync(notification.ProjectId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(notification.TaskId) && !await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.Id == notification.TaskId && item.ProjectId == notification.ProjectId && !item.IsDeleted, cancellationToken))
                return new ProjectManagementNotificationOpenResponse(false, null, "关联任务已删除或你已无权访问");
            var route = string.IsNullOrWhiteSpace(notification.TaskId) ? $"/projects/{notification.ProjectId}/tasks" : $"/projects/{notification.ProjectId}/tasks?selectedTaskId={notification.TaskId}";
            return new ProjectManagementNotificationOpenResponse(true, route, null);
        }
        catch (ValidationException)
        {
            return new ProjectManagementNotificationOpenResponse(false, null, "关联项目已删除或你已无权访问");
        }
        catch (NotFoundException)
        {
            return new ProjectManagementNotificationOpenResponse(false, null, "关联项目已删除或你已无权访问");
        }
    }

    private async Task<ProjectManagementNotificationEntity> GetOwnedAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementNotificationEntity>().Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && item.RecipientUserId == User() && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("通知不存在", ErrorCodes.PlatformResourceNotFound);
    private void EnsureWorkspace(ProjectManagementNotification notification)
    {
        if (!string.Equals(notification.TenantId?.Trim(), Tenant(), StringComparison.Ordinal) || !string.Equals(notification.AppCode?.Trim(), App(), StringComparison.OrdinalIgnoreCase)) throw new ValidationException("通知上下文与当前会话不一致", ErrorCodes.PermissionDenied);
    }
    private static string BuildIdempotencyKey(string type, string recipient, string route, string traceId) => string.Join('\u001f', type, recipient, route, traceId);
    private static bool IsDuplicateNotification(Exception exception) =>
        exception.Message.Contains("pm_notifications", StringComparison.OrdinalIgnoreCase)
        && exception.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    private static ProjectManagementNotificationResponse Map(ProjectManagementNotificationEntity item) => new(item.Id, item.NotificationType, item.Title, item.Message, item.TargetRoute, item.TraceId, item.ProjectId, item.TaskId, item.IsRead, item.CreatedTime, item.ReadTime);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException("通知字段不能为空") : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

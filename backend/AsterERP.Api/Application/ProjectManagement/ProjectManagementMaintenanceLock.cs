using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementMaintenanceLock(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser) : IProjectManagementMaintenanceLock
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<string> AcquireAsync(string lockKey, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(lockKey) ? throw new ValidationException("维护锁标识不能为空") : lockKey.Trim();
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(2)) throw new ValidationException("维护锁时长无效");
        var tenantId = Tenant();
        var appCode = App();
        var operationId = Guid.NewGuid().ToString("N");
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var db = databaseAccessor.GetProjectManagementDb();
            var now = DateTime.UtcNow;
            var existing = await db.Queryable<ProjectManagementMaintenanceLockEntity>()
                .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.LockKey == normalizedKey && !item.IsDeleted && item.ExpiresAt > now)
                .Take(1).ToListAsync(cancellationToken);
            if (existing.Count > 0) throw new ValidationException("当前数据空间正在执行高风险操作，请稍后重试");
            var expired = await db.Queryable<ProjectManagementMaintenanceLockEntity>()
                .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.LockKey == normalizedKey && !item.IsDeleted && item.ExpiresAt <= now)
                .Take(1).ToListAsync(cancellationToken);
            foreach (var stale in expired)
            {
                stale.IsDeleted = true;
                stale.DeletedBy = UserId();
                stale.DeletedTime = now;
                stale.UpdatedBy = stale.DeletedBy;
                stale.UpdatedTime = now;
                await db.Updateable(stale).ExecuteCommandAsync(cancellationToken);
            }
            var entity = new ProjectManagementMaintenanceLockEntity
            {
                TenantId = tenantId, AppCode = appCode, LockKey = normalizedKey, OperationId = operationId,
                OwnerUserId = UserId(), ExpiresAt = now.Add(duration), CreatedBy = UserId(), CreatedTime = now
            };
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            return operationId;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task ReleaseAsync(string operationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationId)) return;
        var db = databaseAccessor.GetProjectManagementDb();
        var rows = await db.Queryable<ProjectManagementMaintenanceLockEntity>()
            .Where(item => item.OperationId == operationId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        var entity = rows.FirstOrDefault();
        if (entity is null) return;
        entity.IsDeleted = true;
        entity.DeletedBy = UserId();
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedBy = entity.DeletedBy;
        entity.UpdatedTime = entity.DeletedTime;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
}

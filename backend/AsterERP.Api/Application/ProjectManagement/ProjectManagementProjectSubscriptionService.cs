using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementProjectSubscriptionService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy) : IProjectManagementProjectSubscriptionService
{
    private static readonly HashSet<string> Modes = ["AllUpdates", "Important", "Mentions"];

    public async Task<ProjectManagementProjectSubscriptionResponse?> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var normalizedProjectId = Required(projectId, "项目标识不能为空");
        await accessPolicy.EnsureCanViewProjectAsync(normalizedProjectId, cancellationToken);
        var entity = await FindAsync(normalizedProjectId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<ProjectManagementProjectSubscriptionResponse> SaveAsync(
        string projectId,
        ProjectManagementProjectSubscriptionUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedProjectId = Required(projectId, "项目标识不能为空");
        await accessPolicy.EnsureCanViewProjectAsync(normalizedProjectId, cancellationToken);
        var mode = NormalizeMode(request.Mode);
        var db = databaseAccessor.GetCurrentDb();
        var entity = await FindAsync(normalizedProjectId, cancellationToken);
        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new ProjectManagementProjectSubscriptionEntity
            {
                TenantId = Tenant(),
                AppCode = App(),
                ProjectId = normalizedProjectId,
                UserId = User(),
                Mode = mode,
                VersionNo = 1,
                CreatedBy = User(),
                CreatedTime = now
            };
            await ProjectManagementMutationTransaction.RunAsync(db, async () => await db.Insertable(entity).ExecuteCommandAsync(cancellationToken));
            return Map(entity);
        }

        EnsureVersion(entity.VersionNo, request.VersionNo);
        entity.Mode = mode;
        entity.VersionNo++;
        entity.UpdatedBy = User();
        entity.UpdatedTime = now;
        await ProjectManagementMutationTransaction.RunAsync(db, async () => await db.Updateable(entity).ExecuteCommandAsync(cancellationToken));
        return Map(entity);
    }

    public async Task DeleteAsync(string projectId, long? versionNo, CancellationToken cancellationToken = default)
    {
        var normalizedProjectId = Required(projectId, "项目标识不能为空");
        await accessPolicy.EnsureCanViewProjectAsync(normalizedProjectId, cancellationToken);
        var entity = await FindAsync(normalizedProjectId, cancellationToken);
        if (entity is null) return;
        EnsureVersion(entity.VersionNo, versionNo);
        var now = DateTime.UtcNow;
        var db = databaseAccessor.GetCurrentDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable<ProjectManagementProjectSubscriptionEntity>()
                .SetColumns(item => new ProjectManagementProjectSubscriptionEntity
                {
                    IsDeleted = true,
                    DeletedBy = User(),
                    DeletedTime = now,
                    UpdatedBy = User(),
                    UpdatedTime = now,
                    VersionNo = entity.VersionNo + 1
                })
                .Where(item => item.Id == entity.Id && item.VersionNo == entity.VersionNo && !item.IsDeleted)
                .ExecuteCommandAsync(cancellationToken);
        });
    }

    private async Task<ProjectManagementProjectSubscriptionEntity?> FindAsync(string projectId, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectSubscriptionEntity>()
            .Where(item => item.ProjectId == projectId && item.UserId == User() && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken)).FirstOrDefault();

    private static ProjectManagementProjectSubscriptionResponse Map(ProjectManagementProjectSubscriptionEntity entity) =>
        new(entity.ProjectId, entity.UserId, entity.Mode, entity.VersionNo, entity.UpdatedTime);

    private static string NormalizeMode(string? mode)
    {
        var normalized = Required(mode, "订阅模式不能为空");
        if (!Modes.Contains(normalized)) throw new ValidationException("订阅模式无效");
        return normalized;
    }

    private static void EnsureVersion(long current, long? requested)
    {
        if (requested.HasValue && requested.Value != current)
            throw new ProjectManagementProjectSubscriptionVersionConflictException(current, requested.Value);
    }

    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
}

public sealed class ProjectManagementProjectSubscriptionVersionConflictException(long serverVersionNo, long clientVersionNo) : Exception("项目订阅版本冲突")
{
    public object Conflict { get; } = new { serverVersionNo, clientVersionNo };
}

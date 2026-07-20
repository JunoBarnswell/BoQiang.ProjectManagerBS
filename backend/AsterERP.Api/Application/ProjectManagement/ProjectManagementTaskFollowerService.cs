using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskFollowerService(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser, ProjectManagementAccessPolicy accessPolicy) : IProjectManagementTaskFollowerService
{
    public async Task<IReadOnlyList<ProjectManagementTaskFollowerResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskFollowerEntity>()
            .Where(item => item.TaskId == taskId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime).ToListAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<ProjectManagementTaskFollowerResponse> AddAsync(string taskId, ProjectManagementTaskFollowerUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        var userId = request.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(userId)) throw new ValidationException("关注人不能为空");
        var db = databaseAccessor.GetCurrentDb();
        var existing = (await db.Queryable<ProjectManagementTaskFollowerEntity>().Where(item => item.TaskId == taskId && item.UserId == userId && item.TenantId == Tenant() && item.AppCode == App()).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        if (existing is { IsDeleted: false }) return Map(existing);
        var entity = existing ?? new ProjectManagementTaskFollowerEntity { TenantId = Tenant(), AppCode = App(), ProjectId = task.ProjectId, TaskId = taskId, UserId = userId, CreatedBy = User(), CreatedTime = DateTime.UtcNow };
        entity.IsDeleted = false; entity.DeletedBy = null; entity.DeletedTime = null; entity.VersionNo = existing is null ? 1 : entity.VersionNo + 1; entity.UpdatedBy = User(); entity.UpdatedTime = DateTime.UtcNow;
        if (existing is null) await db.Insertable(entity).ExecuteCommandAsync(cancellationToken); else await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task RemoveAsync(string taskId, string userId, long versionNo, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await accessPolicy.EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        var entity = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskFollowerEntity>().Where(item => item.TaskId == taskId && item.UserId == userId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("关注人不存在", ErrorCodes.PlatformResourceNotFound);
        if (entity.VersionNo != versionNo) throw new ValidationException("关注关系已被修改，请刷新后重试");
        entity.IsDeleted = true; entity.DeletedBy = User(); entity.DeletedTime = DateTime.UtcNow; entity.UpdatedBy = User(); entity.UpdatedTime = entity.DeletedTime; entity.VersionNo++;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ProjectManagementTaskEntity> GetTaskAsync(string taskId, CancellationToken cancellationToken) => (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == taskId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    private static ProjectManagementTaskFollowerResponse Map(ProjectManagementTaskFollowerEntity item) => new(item.Id, item.TaskId, item.UserId, item.VersionNo, item.CreatedTime);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
}

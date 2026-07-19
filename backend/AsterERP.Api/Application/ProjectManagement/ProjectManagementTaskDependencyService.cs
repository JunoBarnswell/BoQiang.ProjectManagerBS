using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskDependencyService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null) : IProjectManagementTaskDependencyService
{

    public async Task<IReadOnlyList<ProjectManagementTaskDependencyResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageDependenciesAsync(projectId, cancellationToken);
        var items = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<ProjectManagementTaskDependencyResponse> CreateAsync(string projectId, ProjectManagementTaskDependencyUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageDependenciesAsync(projectId, cancellationToken);
        var predecessor = await EnsureTaskAsync(projectId, request.PredecessorTaskId, cancellationToken);
        var successor = await EnsureTaskAsync(projectId, request.SuccessorTaskId, cancellationToken);
        if (predecessor.Id == successor.Id) throw new ValidationException("任务不能依赖自身");
        var type = NormalizeDependencyType(request.DependencyType);
        if (request.LagMinutes < 0) throw new ValidationException("依赖滞后时间不能为负数");
        var db = databaseAccessor.GetProjectManagementDb();
        if (await db.Queryable<ProjectManagementTaskDependencyEntity>().AnyAsync(item => item.ProjectId == projectId && item.PredecessorTaskId == predecessor.Id && item.SuccessorTaskId == successor.Id && !item.IsDeleted, cancellationToken))
            throw new ValidationException("任务依赖已存在");
        var links = await db.Queryable<ProjectManagementTaskDependencyEntity>().Where(item => item.ProjectId == projectId && !item.IsDeleted).ToListAsync(cancellationToken);
        var graph = links.GroupBy(item => item.PredecessorTaskId).ToDictionary(group => group.Key, group => group.Select(item => item.SuccessorTaskId).ToList(), StringComparer.Ordinal);
        if (Reaches(graph, successor.Id, predecessor.Id)) throw new ValidationException("任务依赖不能形成循环");
        var now = DateTime.UtcNow;
        var entity = new ProjectManagementTaskDependencyEntity
        {
            TenantId = RequireTenantId(), AppCode = RequireAppCode(), ProjectId = projectId,
            PredecessorTaskId = predecessor.Id, SuccessorTaskId = successor.Id, DependencyType = type,
            LagMinutes = request.LagMinutes, VersionNo = 1, CreatedBy = RequireUserId(), CreatedTime = now
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await RefreshBlockedStatesAsync(projectId, cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetProjectManagementDb();
        var entity = (await db.Queryable<ProjectManagementTaskDependencyEntity>().Where(item => item.Id == id && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("任务依赖不存在", ErrorCodes.PlatformResourceNotFound);
        if (entity.VersionNo != versionNo || versionNo <= 0) throw new ValidationException("任务依赖已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        entity.IsDeleted = true; entity.DeletedBy = RequireUserId(); entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedBy = entity.DeletedBy; entity.UpdatedTime = entity.DeletedTime; entity.VersionNo++;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await RefreshBlockedStatesAsync(projectId, cancellationToken);
    }

    public async Task RefreshBlockedStatesAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetProjectManagementDb();
        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId && !item.IsDeleted).ToListAsync(cancellationToken);
        var dependencies = await db.Queryable<ProjectManagementTaskDependencyEntity>().Where(item => item.ProjectId == projectId && !item.IsDeleted).ToListAsync(cancellationToken);
        var statusById = tasks.ToDictionary(item => item.Id, item => item.Status, StringComparer.Ordinal);
        var changed = new List<ProjectManagementTaskEntity>();
        foreach (var task in tasks)
        {
            if (task.Status is ProjectManagementDomainRules.TaskDone or ProjectManagementDomainRules.TaskCancelled) continue;
            var blockers = dependencies.Count(item => item.SuccessorTaskId == task.Id && (!statusById.TryGetValue(item.PredecessorTaskId, out var status) || status != ProjectManagementDomainRules.TaskDone));
            if (blockers > 0 && (task.Status is ProjectManagementDomainRules.TaskTodo or ProjectManagementDomainRules.TaskInProgress))
            {
                task.Status = ProjectManagementDomainRules.TaskBlocked;
                task.BlockedReason = $"存在 {blockers} 个未完成前置任务";
                task.VersionNo++;
                task.UpdatedBy = RequireUserId();
                task.UpdatedTime = DateTime.UtcNow;
                changed.Add(task);
            }
            else if (blockers == 0 && task.Status == ProjectManagementDomainRules.TaskBlocked && task.BlockedReason?.Contains("前置任务", StringComparison.Ordinal) == true)
            {
                task.Status = ProjectManagementDomainRules.TaskTodo;
                task.BlockedReason = null;
                task.VersionNo++;
                task.UpdatedBy = RequireUserId();
                task.UpdatedTime = DateTime.UtcNow;
                changed.Add(task);
            }
        }
        if (changed.Count > 0) await db.Updateable(changed).ExecuteCommandAsync(cancellationToken);
    }

    private async Task EnsureProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        RequireTenantId(); RequireAppCode();
        if (!await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && !item.IsDeleted).AnyAsync(cancellationToken))
            throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementTaskEntity> EnsureTaskAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        var task = (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == id && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        return task ?? throw new ValidationException("依赖任务不存在或不属于当前项目");
    }

    private static bool Reaches(Dictionary<string, List<string>> graph, string start, string target)
    {
        var queue = new Queue<string>([start]);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;
            if (current == target) return true;
            if (graph.TryGetValue(current, out var next)) foreach (var item in next) queue.Enqueue(item);
        }
        return false;
    }

    private static string NormalizeDependencyType(string value) => ProjectManagementDomainRules.RequireDependencyType(value);
    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private static string RequireAppCode() => ProjectManagementPlatformScope.AppCode;
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static ProjectManagementTaskDependencyResponse Map(ProjectManagementTaskDependencyEntity entity) => new(entity.Id, entity.ProjectId, entity.PredecessorTaskId, entity.SuccessorTaskId, entity.DependencyType, entity.LagMinutes, entity.VersionNo);
}

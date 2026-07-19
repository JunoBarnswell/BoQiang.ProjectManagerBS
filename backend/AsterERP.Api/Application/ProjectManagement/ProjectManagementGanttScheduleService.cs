using System.Diagnostics;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>甘特拖动/缩放的专属原子排程服务。所有候选日期先在内存快照上验证，成功后才在同一数据库事务写入。</summary>
public sealed class ProjectManagementGanttScheduleService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy,
    IProjectManagementActivityWriter? activityWriter = null) : IProjectManagementGanttScheduleService, ITransientDependency
{
    private const int MaxItems = 200;

    public async Task<ProjectManagementGanttScheduleBatchUpdateResponse> UpdateAsync(ProjectManagementGanttScheduleBatchUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectId)) throw new ValidationException("项目不能为空");
        if (request.Items is null || request.Items.Count is < 1 or > MaxItems) throw new ValidationException($"甘特排程任务数量必须在 1 到 {MaxItems} 之间");
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var projectId = request.ProjectId.Trim();
        var ids = request.Items.Select(item => item.TaskId?.Trim() ?? string.Empty).ToList();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) throw new ValidationException("排程任务不能为空且不能重复");
        foreach (var item in request.Items) ProjectManagementDomainRules.ValidateDates(item.StartDate, item.DueDate, "任务");

        await accessPolicy.EnsureCanViewProjectAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var project = (await db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
        var allTasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .Take(20_001).ToListAsync(cancellationToken);
        if (allTasks.Count > 20_000) throw new ValidationException("项目任务数量超过甘特排程上限 20000");
        var byId = allTasks.ToDictionary(item => item.Id, StringComparer.Ordinal);
        if (ids.Any(id => !byId.ContainsKey(id))) throw new ValidationException("存在不属于当前项目、已删除或无权访问的任务");
        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var task = byId[item.TaskId.Trim()];
            await accessPolicy.EnsureCanManageTaskAsync(projectId, task.Id, task.ParentTaskId, task.AssigneeUserId, cancellationToken: cancellationToken);
            if (item.VersionNo <= 0 || task.VersionNo != item.VersionNo)
                throw new ValidationException($"任务 {task.TaskCode} 已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
            EnsureWithinProjectBoundary(project, item);
        }

        var planned = allTasks.ToDictionary(item => item.Id, item => new PlannedDates(item.StartDate, item.DueDate), StringComparer.Ordinal);
        foreach (var item in request.Items) planned[item.TaskId.Trim()] = new(item.StartDate, item.DueDate);
        var dependencies = await db.Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .Take(40_001).ToListAsync(cancellationToken);
        if (dependencies.Count > 40_000) throw new ValidationException("项目依赖数量超过甘特排程上限 40000");
        ValidateFinishToStartDependencies(dependencies, planned, cancellationToken);

        var actor = RequireUserId();
        var now = DateTime.UtcNow;
        var changed = request.Items.Select(item => byId[item.TaskId.Trim()]).ToList();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            foreach (var item in request.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var task = byId[item.TaskId.Trim()];
                task.StartDate = item.StartDate;
                task.DueDate = item.DueDate;
                task.VersionNo++;
                task.UpdatedBy = actor;
                task.UpdatedTime = now;
            }
            await db.Updateable(changed).ExecuteCommandAsync(cancellationToken);
            if (activityWriter is not null)
                foreach (var task in changed)
                    await activityWriter.AppendAsync(new ProjectManagementActivityEvent(tenantId, appCode, "Task", task.Id, "task.gantt-scheduled",
                        "通过甘特图调整计划日期", Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), actor, projectId), cancellationToken);
        });
        return new(projectId, changed.Select(item => new ProjectManagementGanttScheduleTaskResult(item.Id, item.StartDate!.Value, item.DueDate!.Value, item.VersionNo)).ToList());
    }

    private static void ValidateFinishToStartDependencies(IEnumerable<ProjectManagementTaskDependencyEntity> dependencies, IReadOnlyDictionary<string, PlannedDates> planned, CancellationToken cancellationToken)
    {
        foreach (var dependency in dependencies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(dependency.DependencyType, "FinishToStart", StringComparison.Ordinal)) continue;
            if (!planned.TryGetValue(dependency.PredecessorTaskId, out var predecessor) || !planned.TryGetValue(dependency.SuccessorTaskId, out var successor)) continue;
            if (!predecessor.DueDate.HasValue || !successor.StartDate.HasValue) continue;
            if (successor.StartDate.Value < predecessor.DueDate.Value.AddMinutes(dependency.LagMinutes))
                throw new ValidationException($"后置任务不能早于前置任务完成：{dependency.PredecessorTaskId} -> {dependency.SuccessorTaskId}");
        }
    }

    private static void EnsureWithinProjectBoundary(ProjectManagementProjectEntity project, ProjectManagementGanttScheduleTaskChange item)
    {
        if (project.StartDate.HasValue && item.StartDate < project.StartDate.Value) throw new ValidationException("任务开始日期不能早于项目开始日期");
        if (project.DueDate.HasValue && item.DueDate > project.DueDate.Value) throw new ValidationException("任务截止日期不能晚于项目截止日期");
    }

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private sealed record PlannedDates(DateTime? StartDate, DateTime? DueDate);
}

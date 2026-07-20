using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 以整分钟维护任务实际工时。日志变动和任务汇总始终处于同一个事务中，
/// 且汇总从当前有效日志重建，避免删除或修改边界日志后留下过期的起止时间。
/// </summary>
public sealed class ProjectManagementTaskTimeLogService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementDisplayProjectionService? displayProjection = null) : IProjectManagementTaskTimeLogService
{
    public async Task<IReadOnlyList<ProjectManagementTaskTimeLogResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await Policy().EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        return (await databaseAccessor.GetCurrentDb()
            .Queryable<ProjectManagementTaskTimeLogEntity>()
            .Where(item => item.TaskId == taskId && !item.IsDeleted)
            .OrderBy(item => item.StartedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken))
            .Select(Map)
            .ToList();
    }

    public async Task<ProjectManagementTaskTimeLogResponse> CreateAsync(string taskId, ProjectManagementTaskTimeLogUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        EnsureVersion(task.VersionNo, request.VersionNo);
        var (startedAt, endedAt, minutes) = NormalizeDuration(request.StartedAt, request.EndedAt);
        var now = DateTime.UtcNow;
        var log = new ProjectManagementTaskTimeLogEntity
        {
            TenantId = Tenant(),
            AppCode = App(),
            ProjectId = task.ProjectId,
            TaskId = taskId,
            UserId = User(),
            StartedAt = startedAt,
            EndedAt = endedAt,
            Minutes = minutes,
            Note = Optional(request.Note),
            CreatedBy = User(),
            CreatedTime = now
        };
        var db = databaseAccessor.GetCurrentDb();
        db.Ado.BeginTran();
        try
        {
            await db.Insertable(log).ExecuteCommandAsync(cancellationToken);
            await RefreshTaskActualTimeAsync(db, task, cancellationToken);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }

        return Map(log);
    }

    public async Task<ProjectManagementTaskTimeLogResponse> UpdateAsync(string taskId, string id, ProjectManagementTaskTimeLogUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        EnsureVersion(task.VersionNo, request.TaskVersionNo);
        var db = databaseAccessor.GetCurrentDb();
        var log = await GetActiveLogAsync(db, taskId, id, cancellationToken);
        EnsureVersion(log.VersionNo, request.VersionNo);
        var (startedAt, endedAt, minutes) = NormalizeDuration(request.StartedAt, request.EndedAt);
        log.StartedAt = startedAt;
        log.EndedAt = endedAt;
        log.Minutes = minutes;
        log.Note = Optional(request.Note);
        log.UpdatedBy = User();
        log.UpdatedTime = DateTime.UtcNow;
        log.VersionNo++;

        db.Ado.BeginTran();
        try
        {
            await db.Updateable(log)
                .UpdateColumns(item => new { item.StartedAt, item.EndedAt, item.Minutes, item.Note, item.UpdatedBy, item.UpdatedTime, item.VersionNo })
                .ExecuteCommandAsync(cancellationToken);
            await RefreshTaskActualTimeAsync(db, task, cancellationToken);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }

        return Map(log);
    }

    public async Task DeleteAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var log = await GetActiveLogAsync(db, taskId, id, cancellationToken);
        EnsureVersion(log.VersionNo, versionNo);
        log.IsDeleted = true;
        log.DeletedBy = User();
        log.DeletedTime = DateTime.UtcNow;
        log.VersionNo++;

        db.Ado.BeginTran();
        try
        {
            await db.Updateable(log)
                .UpdateColumns(item => new { item.IsDeleted, item.DeletedBy, item.DeletedTime, item.VersionNo })
                .ExecuteCommandAsync(cancellationToken);
            await RefreshTaskActualTimeAsync(db, task, cancellationToken);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    public async Task<IReadOnlyList<ProjectManagementTaskWorkloadResponse>> QueryWorkloadAsync(ProjectManagementTaskWorkloadQuery query, CancellationToken cancellationToken = default)
    {
        var projectId = RequireProjectId(query.ProjectId);
        if (query.TimeLogStartedFrom.HasValue && query.TimeLogStartedTo.HasValue && query.TimeLogStartedFrom > query.TimeLogStartedTo)
            throw new ValidationException("工时日志筛选区间无效");

        await Policy().EnsureCanViewProjectAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var tenantId = Tenant();
        var appCode = App();
        var now = DateTime.UtcNow;

        var taskAggregates = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(task => task.ProjectId == projectId && task.TenantId == tenantId && task.AppCode == appCode && !task.IsDeleted && task.AssigneeUserId != null && task.AssigneeUserId != "")
            .GroupBy(task => task.AssigneeUserId)
            .Select(task => new ProjectManagementTaskWorkloadTaskAggregate
            {
                UserId = task.AssigneeUserId!,
                TodoTaskCount = SqlFunc.AggregateSum(task.Status == ProjectManagementDomainRules.TaskTodo ? 1 : 0),
                InProgressTaskCount = SqlFunc.AggregateSum(task.Status == ProjectManagementDomainRules.TaskInProgress ? 1 : 0),
                CompletedTaskCount = SqlFunc.AggregateSum(task.Status == ProjectManagementDomainRules.TaskDone ? 1 : 0),
                OverdueTaskCount = SqlFunc.AggregateSum(task.DueDate != null && task.DueDate < now && task.Status != ProjectManagementDomainRules.TaskDone && task.Status != ProjectManagementDomainRules.TaskCancelled ? 1 : 0),
                EstimatedMinutes = SqlFunc.AggregateSum(task.EstimateMinutes ?? 0)
            })
            .ToListAsync(cancellationToken);

        var logs = db.Queryable<ProjectManagementTaskTimeLogEntity>()
            .Where(log => log.ProjectId == projectId && log.TenantId == tenantId && log.AppCode == appCode && !log.IsDeleted);
        if (query.TimeLogStartedFrom.HasValue) logs = logs.Where(log => log.StartedAt >= query.TimeLogStartedFrom.Value);
        if (query.TimeLogStartedTo.HasValue) logs = logs.Where(log => log.StartedAt <= query.TimeLogStartedTo.Value);
        var logAggregates = await logs
            .GroupBy(log => log.UserId)
            .Select(log => new ProjectManagementTaskWorkloadLogAggregate
            {
                UserId = log.UserId,
                LoggedMinutes = SqlFunc.AggregateSum(log.Minutes)
            })
            .ToListAsync(cancellationToken);

        var workload = ProjectManagementTaskWorkloadProjection.Create(taskAggregates, logAggregates);
        var displays = await (displayProjection ?? new ProjectManagementDisplayProjectionService(databaseAccessor))
            .ResolveAsync([], [], workload.Select(item => item.UserId), cancellationToken);
        return workload.Select(item => item with { DisplayName = displays.User(item.UserId) }).ToList();
    }

    private async Task RefreshTaskActualTimeAsync(ISqlSugarClient db, ProjectManagementTaskEntity task, CancellationToken cancellationToken)
    {
        var aggregates = await db.Queryable<ProjectManagementTaskTimeLogEntity>()
            .Where(log => log.TaskId == task.Id && !log.IsDeleted)
            .GroupBy(log => log.TaskId)
            .Select(log => new TaskActualTimeAggregate
            {
                Minutes = SqlFunc.AggregateSum(log.Minutes),
                StartedAt = SqlFunc.AggregateMin(log.StartedAt),
                EndedAt = SqlFunc.AggregateMax(log.EndedAt)
            })
            .ToListAsync(cancellationToken);
        var aggregate = aggregates.FirstOrDefault();
        task.ActualMinutes = aggregate?.Minutes ?? 0;
        task.ActualStartAt = aggregate?.StartedAt;
        task.ActualEndAt = aggregate?.EndedAt;
        task.VersionNo++;
        await db.Updateable(task)
            .UpdateColumns(item => new { item.ActualMinutes, item.ActualStartAt, item.ActualEndAt, item.VersionNo })
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ProjectManagementTaskEntity> EnsureTaskAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault() ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);

    private static async Task<ProjectManagementTaskTimeLogEntity> GetActiveLogAsync(ISqlSugarClient db, string taskId, string id, CancellationToken cancellationToken) =>
        (await db.Queryable<ProjectManagementTaskTimeLogEntity>()
            .Where(item => item.Id == id && item.TaskId == taskId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault() ?? throw new NotFoundException("工时记录不存在", ErrorCodes.PlatformResourceNotFound);

    private ProjectManagementAccessPolicy Policy() => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string RequireProjectId(string value) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException("项目标识不能为空") : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureVersion(long current, long request)
    {
        if (request <= 0 || current != request)
            throw new ValidationException("任务或工时已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
    }

    private static (DateTime StartedAt, DateTime EndedAt, int Minutes) NormalizeDuration(DateTime startedAt, DateTime endedAt)
    {
        if (endedAt <= startedAt) throw new ValidationException("工时结束时间必须晚于开始时间");
        if (startedAt.Second != 0 || startedAt.Millisecond != 0 || endedAt.Second != 0 || endedAt.Millisecond != 0)
            throw new ValidationException("工时日志必须精确到整分钟");
        var minutes = (endedAt - startedAt).TotalMinutes;
        if (minutes != Math.Truncate(minutes)) throw new ValidationException("工时日志必须精确到整分钟");
        if (minutes is < 1 or > 24 * 60) throw new ValidationException("单次工时必须在 1 分钟到 24 小时之间");
        return (startedAt, endedAt, checked((int)minutes));
    }

    private static ProjectManagementTaskTimeLogResponse Map(ProjectManagementTaskTimeLogEntity entity) =>
        new(entity.Id, entity.TaskId, entity.UserId, entity.StartedAt, entity.EndedAt, entity.Minutes, entity.Note, entity.VersionNo);

    private sealed class TaskActualTimeAggregate
    {
        public int Minutes { get; init; }
        public DateTime StartedAt { get; init; }
        public DateTime EndedAt { get; init; }
    }
}

using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskTimeLogService(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser, ProjectManagementAccessPolicy? accessPolicy = null) : IProjectManagementTaskTimeLogService
{
    public async Task<IReadOnlyList<ProjectManagementTaskTimeLogResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await EnsureTaskAsync(taskId, cancellationToken);
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskTimeLogEntity>().Where(item => item.TaskId == taskId && !item.IsDeleted).OrderBy(item => item.StartedAt, SqlSugar.OrderByType.Desc).ToListAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<ProjectManagementTaskTimeLogResponse> CreateAsync(string taskId, ProjectManagementTaskTimeLogUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, task.Id, cancellationToken: cancellationToken);
        if (request.EndedAt <= request.StartedAt) throw new ValidationException("工时结束时间必须晚于开始时间");
        var minutes = (int)Math.Ceiling((request.EndedAt - request.StartedAt).TotalMinutes);
        if (minutes <= 0 || minutes > 24 * 60) throw new ValidationException("单次工时必须在 1 分钟到 24 小时之间");
        EnsureVersion(task.VersionNo, request.VersionNo);
        var now = DateTime.UtcNow;
        var log = new ProjectManagementTaskTimeLogEntity { TenantId = Tenant(), AppCode = App(), ProjectId = task.ProjectId, TaskId = taskId, UserId = User(), StartedAt = request.StartedAt, EndedAt = request.EndedAt, Minutes = minutes, Note = Optional(request.Note), CreatedBy = User(), CreatedTime = now };
        var db = databaseAccessor.GetCurrentDb();
        db.Ado.BeginTran();
        try
        {
            await db.Insertable(log).ExecuteCommandAsync(cancellationToken);
            task.ActualMinutes += minutes;
            task.ActualStartAt = task.ActualStartAt.HasValue && task.ActualStartAt.Value <= request.StartedAt ? task.ActualStartAt : request.StartedAt;
            task.ActualEndAt = task.ActualEndAt.HasValue && task.ActualEndAt.Value >= request.EndedAt ? task.ActualEndAt : request.EndedAt;
            task.VersionNo++;
            await db.Updateable(task).UpdateColumns(item => new { item.ActualMinutes, item.ActualStartAt, item.ActualEndAt, item.VersionNo }).ExecuteCommandAsync(cancellationToken);
            db.Ado.CommitTran();
        }
        catch { db.Ado.RollbackTran(); throw; }
        return Map(log);
    }

    public async Task DeleteAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, task.Id, cancellationToken: cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var log = (await db.Queryable<ProjectManagementTaskTimeLogEntity>().Where(item => item.Id == id && item.TaskId == taskId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("工时记录不存在", ErrorCodes.PlatformResourceNotFound);
        EnsureVersion(log.VersionNo, versionNo);
        log.IsDeleted = true; log.DeletedBy = User(); log.DeletedTime = DateTime.UtcNow; log.VersionNo++;
        db.Ado.BeginTran();
        try
        {
            await db.Updateable(log).ExecuteCommandAsync(cancellationToken);
            task.ActualMinutes = Math.Max(0, task.ActualMinutes - log.Minutes); task.VersionNo++;
            await db.Updateable(task).UpdateColumns(item => new { item.ActualMinutes, item.VersionNo }).ExecuteCommandAsync(cancellationToken);
            db.Ado.CommitTran();
        }
        catch { db.Ado.RollbackTran(); throw; }
    }

    private async Task<ProjectManagementTaskEntity> EnsureTaskAsync(string id, CancellationToken cancellationToken) => (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == id && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureVersion(long current, long request) { if (request <= 0 || current != request) throw new ValidationException("任务或工时已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static ProjectManagementTaskTimeLogResponse Map(ProjectManagementTaskTimeLogEntity entity) => new(entity.Id, entity.TaskId, entity.UserId, entity.StartedAt, entity.EndedAt, entity.Minutes, entity.Note, entity.VersionNo);
}

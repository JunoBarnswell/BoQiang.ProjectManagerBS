using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskParticipantService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementMemberCandidateService candidateService,
    IProjectManagementImConversationService? imConversationService = null,
    ProjectManagementAccessPolicy? accessPolicy = null) : IProjectManagementTaskParticipantService
{
    public async Task<IReadOnlyList<ProjectManagementTaskParticipantResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskParticipantEntity>().Where(item => item.TaskId == taskId && !item.IsDeleted).OrderBy(item => item.CreatedTime).ToListAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<ProjectManagementTaskParticipantResponse> AddAsync(string taskId, ProjectManagementTaskParticipantUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        EnsureVersion(task.VersionNo, request.VersionNo);
        var userId = Required(request.UserId, "参与人不能为空");
        if (!await candidateService.IsSelectableAsync(userId, cancellationToken)) throw new ValidationException("参与人必须来自当前租户和应用的启用用户/任职");
        var db = databaseAccessor.GetCurrentDb();
        if (!await db.Queryable<ProjectManagementProjectMemberEntity>().AnyAsync(item => item.ProjectId == task.ProjectId && item.UserId == userId && item.IsActive && !item.IsDeleted, cancellationToken)) throw new ValidationException("参与人必须先加入项目");
        if (await db.Queryable<ProjectManagementTaskParticipantEntity>().AnyAsync(item => item.TaskId == taskId && item.UserId == userId && !item.IsDeleted, cancellationToken)) throw new ValidationException("该参与人已存在");
        var entity = new ProjectManagementTaskParticipantEntity { TenantId = Tenant(), AppCode = App(), ProjectId = task.ProjectId, TaskId = taskId, UserId = userId, EmploymentId = Optional(request.EmploymentId), RoleCode = Required(request.RoleCode, "参与人角色不能为空"), CreatedBy = User(), CreatedTime = DateTime.UtcNow };
        db.Ado.BeginTran();
        try { await db.Insertable(entity).ExecuteCommandAsync(cancellationToken); task.VersionNo++; await db.Updateable(task).UpdateColumns(item => new { item.VersionNo }).ExecuteCommandAsync(cancellationToken); db.Ado.CommitTran(); } catch { db.Ado.RollbackTran(); throw; }
        if (imConversationService is not null)
        {
            await imConversationService.SynchronizeTaskLinksAsync(taskId, cancellationToken);
        }
        return Map(entity);
    }

    public async Task RemoveAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        EnsureVersion(task.VersionNo, versionNo);
        var db = databaseAccessor.GetCurrentDb();
        var entity = (await db.Queryable<ProjectManagementTaskParticipantEntity>().Where(item => item.Id == id && item.TaskId == taskId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("任务参与人不存在", ErrorCodes.PlatformResourceNotFound);
        if (imConversationService is not null)
        {
            await imConversationService.RevokeTaskParticipantAsync(taskId, entity.UserId, cancellationToken);
        }
        entity.IsDeleted = true; entity.DeletedBy = User(); entity.DeletedTime = DateTime.UtcNow; entity.VersionNo++;
        db.Ado.BeginTran();
        try { await db.Updateable(entity).ExecuteCommandAsync(cancellationToken); task.VersionNo++; await db.Updateable(task).UpdateColumns(item => new { item.VersionNo }).ExecuteCommandAsync(cancellationToken); db.Ado.CommitTran(); } catch { db.Ado.RollbackTran(); throw; }
    }

    private async Task<ProjectManagementTaskEntity> EnsureTaskAsync(string id, CancellationToken cancellationToken) => (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == id && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureVersion(long current, long request) { if (current != request || request <= 0) throw new ValidationException("任务已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static ProjectManagementTaskParticipantResponse Map(ProjectManagementTaskParticipantEntity entity) => new(entity.Id, entity.TaskId, entity.UserId, entity.EmploymentId, entity.RoleCode, entity.VersionNo);
}

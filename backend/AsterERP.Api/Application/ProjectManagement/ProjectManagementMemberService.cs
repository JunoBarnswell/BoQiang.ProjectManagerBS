using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Diagnostics;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementMemberService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementMemberCandidateService candidateService,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null,
    IProjectManagementImConversationService? imConversationService = null,
    IProjectManagementActivityWriter? activityWriter = null) : IProjectManagementMemberService
{

    public async Task<GridPageResult<ProjectManagementMemberResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode() && !item.IsDeleted && item.IsActive)
            .OrderBy(item => item.JoinedAt, OrderByType.Asc)
            .ToPageListAsync(1, 500, total, cancellationToken);
        return new GridPageResult<ProjectManagementMemberResponse> { Total = total.Value, Items = items.Select(Map).ToList() };
    }

    public async Task<ProjectManagementMemberResponse> AddAsync(string projectId, ProjectManagementMemberUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageMembersAsync(projectId, cancellationToken);
        var userId = NormalizeRequired(request.UserId, "成员用户不能为空");
        var roleCode = NormalizeRole(request.RoleCode);
        var employmentId = NormalizeOptional(request.EmploymentId);
        if (!await candidateService.IsSelectableAsync(userId, employmentId, cancellationToken))
            throw new ValidationException("成员必须来自当前租户和应用的启用用户/任职");
        var db = databaseAccessor.GetCurrentDb();
        var existing = await db.Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode() && item.UserId == userId)
            .Take(1)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0 && !existing[0].IsDeleted)
        {
            throw new ValidationException("该用户已经是项目成员");
        }

        var now = DateTime.UtcNow;
        var entity = existing.FirstOrDefault() ?? new ProjectManagementProjectMemberEntity
        {
            ProjectId = projectId,
            UserId = userId,
            TenantId = RequireTenantId(),
            AppCode = RequireAppCode(),
            CreatedBy = RequireUserId(),
            CreatedTime = now
        };
        entity.EmploymentId = employmentId;
        entity.RoleCode = roleCode;
        entity.ScopeRootTaskId = await ValidateScopeRootTaskAsync(projectId, roleCode, request.ScopeRootTaskId, cancellationToken);
        entity.IsActive = true;
        entity.JoinedAt = entity.JoinedAt == default ? now : entity.JoinedAt;
        entity.LeftAt = null;
        entity.IsDeleted = false;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
        entity.VersionNo = entity.VersionNo <= 0 ? 1 : entity.VersionNo + 1;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = now;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            if (existing.Count == 0)
                await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            else
                await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "project.member.added", BuildAddedSummary(entity), cancellationToken);
        });
        await PublishInvalidationAsync(entity, "project.member.added", cancellationToken);
        if (imConversationService is not null)
        {
            await imConversationService.SynchronizeProjectLinksAsync(projectId, cancellationToken);
        }
        return Map(entity);
    }

    public async Task<ProjectManagementMemberResponse> UpdateAsync(string projectId, string id, ProjectManagementMemberUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(projectId, id, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageMembersAsync(projectId, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var userId = NormalizeRequired(request.UserId, "成员用户不能为空");
        if (!string.Equals(entity.UserId, userId, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("项目成员不支持变更用户，请移除后重新添加");
        var employmentId = NormalizeOptional(request.EmploymentId);
        if (!await candidateService.IsSelectableAsync(entity.UserId, employmentId, cancellationToken))
            throw new ValidationException("成员必须来自当前租户和应用的启用用户/任职");
        var nextRole = NormalizeRole(request.RoleCode);
        await EnsureOwnerWillRemainAsync(entity, nextRole, cancellationToken);
        var nextScopeRootTaskId = await ValidateScopeRootTaskAsync(projectId, nextRole, request.ScopeRootTaskId, cancellationToken);
        var before = Snapshot(entity);
        entity.EmploymentId = employmentId;
        entity.RoleCode = nextRole;
        entity.ScopeRootTaskId = nextScopeRootTaskId;
        entity.VersionNo++;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetCurrentDb(), async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "project.member.updated", BuildUpdatedSummary(before, entity), cancellationToken);
        });
        await PublishInvalidationAsync(entity, "project.member.updated", cancellationToken);
        if (imConversationService is not null)
        {
            await imConversationService.SynchronizeProjectLinksAsync(projectId, cancellationToken);
        }
        return Map(entity);
    }

    public async Task RemoveAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(projectId, id, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageMembersAsync(projectId, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        await EnsureOwnerWillRemainAsync(entity, null, cancellationToken);
        if (imConversationService is not null)
        {
            await imConversationService.RevokeProjectMemberAsync(projectId, entity.UserId, cancellationToken);
        }
        var before = Snapshot(entity);
        entity.IsActive = false;
        entity.IsDeleted = true;
        entity.LeftAt = DateTime.UtcNow;
        entity.DeletedBy = RequireUserId();
        entity.DeletedTime = entity.LeftAt;
        entity.UpdatedBy = entity.DeletedBy;
        entity.UpdatedTime = entity.LeftAt;
        entity.VersionNo++;
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetCurrentDb(), async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "project.member.removed", BuildRemovedSummary(before), cancellationToken);
        });
        if (realtimePublisher is not null)
        {
            await realtimePublisher.RevokeProjectAccessAsync(RequireTenantId(), RequireAppCode(), projectId, entity.UserId, cancellationToken);
            await PublishInvalidationAsync(entity, "project.member.removed", cancellationToken);
        }
    }

    private async Task EnsureProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        RequireTenantId();
        RequireAppCode();
        var exists = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode() && !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (!exists) throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementProjectMemberEntity> GetRequiredAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var entity = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.Id == id && item.ProjectId == projectId && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode() && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken);
        return entity.FirstOrDefault() ?? throw new NotFoundException("项目成员不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task EnsureOwnerWillRemainAsync(ProjectManagementProjectMemberEntity entity, string? nextRole, CancellationToken cancellationToken)
    {
        if (!string.Equals(entity.RoleCode, "Owner", StringComparison.Ordinal) || string.Equals(nextRole, "Owner", StringComparison.Ordinal)) return;
        var ownerCount = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == entity.ProjectId && item.RoleCode == "Owner" && item.IsActive && !item.IsDeleted && item.Id != entity.Id)
            .CountAsync(cancellationToken);
        if (ownerCount == 0) throw new ValidationException("项目至少需要保留一个有效 Owner");
    }

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string RequireAppCode()
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        return ProjectManagementPlatformScope.AppCode;
    }
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string NormalizeRequired(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeRole(string value) => ProjectManagementDomainRules.RequireRole(value);
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("项目成员已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private async Task PublishInvalidationAsync(ProjectManagementProjectMemberEntity entity, string eventType, CancellationToken cancellationToken)
    {
        if (realtimePublisher is null) return;
        await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(RequireTenantId(), RequireAppCode(), "ProjectMember", entity.Id, eventType, entity.VersionNo, Guid.NewGuid().ToString("N"), entity.ProjectId), cancellationToken);
    }
    private async Task<string?> ValidateScopeRootTaskAsync(string projectId, string roleCode, string? requestedScopeRootTaskId, CancellationToken cancellationToken)
    {
        var scopeRootTaskId = NormalizeOptional(requestedScopeRootTaskId);
        if (scopeRootTaskId is null) return null;
        if (!string.Equals(roleCode, "Lead", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("只有 Lead 可以绑定主题父任务范围");
        var validRoot = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(task => task.Id == scopeRootTaskId && task.ProjectId == projectId && task.TenantId == RequireTenantId() && task.AppCode == RequireAppCode() &&
                task.ParentTaskId == null && task.Depth == 0 && !task.IsDeleted)
            .AnyAsync(cancellationToken);
        if (!validRoot) throw new ValidationException("主题父任务不存在、不属于当前项目或不是主题根节点");
        return scopeRootTaskId;
    }
    private async Task WriteActivityAsync(ProjectManagementProjectMemberEntity entity, string activityType, string summary, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(RequireTenantId(), RequireAppCode(), "ProjectMember", entity.Id, activityType, summary, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), entity.ProjectId), cancellationToken);
    }
    private static MemberSnapshot Snapshot(ProjectManagementProjectMemberEntity entity) => new(entity.UserId, entity.EmploymentId, entity.RoleCode, entity.ScopeRootTaskId, entity.IsActive);
    private static string BuildAddedSummary(ProjectManagementProjectMemberEntity entity) => $"添加成员 user={entity.UserId}; employment={Display(entity.EmploymentId)}; role={entity.RoleCode}; scope={Display(entity.ScopeRootTaskId)}";
    private static string BuildUpdatedSummary(MemberSnapshot before, ProjectManagementProjectMemberEntity after) => $"更新成员 user={after.UserId}; employment={Display(before.EmploymentId)}->{Display(after.EmploymentId)}; role={before.RoleCode}->{after.RoleCode}; scope={Display(before.ScopeRootTaskId)}->{Display(after.ScopeRootTaskId)}; active={before.IsActive}->{after.IsActive}";
    private static string BuildRemovedSummary(MemberSnapshot before) => $"移除成员 user={before.UserId}; employment={Display(before.EmploymentId)}; role={before.RoleCode}; scope={Display(before.ScopeRootTaskId)}";
    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    private sealed record MemberSnapshot(string UserId, string? EmploymentId, string RoleCode, string? ScopeRootTaskId, bool IsActive);
    private static ProjectManagementMemberResponse Map(ProjectManagementProjectMemberEntity entity) => new(entity.Id, entity.ProjectId, entity.UserId, entity.EmploymentId, entity.RoleCode, entity.ScopeRootTaskId, entity.IsActive, entity.JoinedAt, entity.LeftAt, entity.VersionNo);
}

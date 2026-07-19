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
        await AccessPolicy.EnsureCanViewProjectAsync(projectId, cancellationToken);
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted && item.IsActive)
            .OrderBy(item => item.JoinedAt, OrderByType.Asc)
            .ToPageListAsync(1, 500, total, cancellationToken);
        return new GridPageResult<ProjectManagementMemberResponse> { Total = total.Value, Items = items.Select(Map).ToList() };
    }

    public async Task<ProjectManagementMemberResponse> AddAsync(string projectId, ProjectManagementMemberUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await AccessPolicy.EnsureCanManageMembersAsync(projectId, cancellationToken);
        var userId = NormalizeRequired(request.UserId, "成员用户不能为空");
        var roleCode = NormalizeRole(request.RoleCode);
        var employmentId = NormalizeOptional(request.EmploymentId);
        if (!await candidateService.IsSelectableAsync(userId, employmentId, cancellationToken))
            throw new ValidationException("成员必须来自当前租户和应用的启用用户/任职");
        var db = databaseAccessor.GetCurrentDb();
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var existing = await db.Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && item.UserId == userId)
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
            TenantId = tenantId,
            AppCode = appCode,
            CreatedBy = RequireUserId(),
            CreatedTime = now
        };
        entity.EmploymentId = employmentId;
        entity.RoleCode = roleCode;
        entity.ScopeRootTaskId = await NormalizeScopeRootTaskIdAsync(projectId, roleCode, request.ScopeRootTaskId, cancellationToken);
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
            await WriteActivityAsync(entity, existing.Count == 0 ? "added" : "restored", existing.Count == 0 ? $"添加项目成员 {entity.UserId}" : $"恢复项目成员 {entity.UserId}", CreateChanges(null, entity), now, cancellationToken);
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
        await AccessPolicy.EnsureCanManageMembersAsync(projectId, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var nextUserId = NormalizeRequired(request.UserId, "成员用户不能为空");
        var nextEmploymentId = NormalizeOptional(request.EmploymentId);
        if (!await candidateService.IsSelectableAsync(nextUserId, nextEmploymentId, cancellationToken))
            throw new ValidationException("成员必须来自当前租户和应用的启用用户/任职");
        var nextRole = NormalizeRole(request.RoleCode);
        var db = databaseAccessor.GetCurrentDb();
        var duplicate = await db.Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode() && item.UserId == nextUserId && item.Id != entity.Id && !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (duplicate) throw new ValidationException("该用户已经是项目成员");
        await EnsureOwnerWillRemainAsync(entity, nextRole, cancellationToken);
        var before = MemberActivitySnapshot.From(entity);
        entity.UserId = nextUserId;
        entity.EmploymentId = nextEmploymentId;
        entity.RoleCode = nextRole;
        entity.ScopeRootTaskId = await NormalizeScopeRootTaskIdAsync(projectId, nextRole, request.ScopeRootTaskId, cancellationToken);
        entity.VersionNo++;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "updated", $"更新项目成员 {entity.UserId}", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, cancellationToken);
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
        await AccessPolicy.EnsureCanManageMembersAsync(projectId, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        await EnsureOwnerWillRemainAsync(entity, null, cancellationToken);
        if (imConversationService is not null)
        {
            await imConversationService.RevokeProjectMemberAsync(projectId, entity.UserId, cancellationToken);
        }
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
            await WriteActivityAsync(entity, "removed", $"移除项目成员 {entity.UserId}", CreateChanges(MemberActivitySnapshot.From(entity) with { IsActive = true, IsDeleted = false, LeftAt = null }, entity), entity.LeftAt ?? DateTime.UtcNow, cancellationToken);
        });
        if (realtimePublisher is not null)
        {
            await realtimePublisher.RevokeProjectAccessAsync(RequireTenantId(), RequireAppCode(), projectId, entity.UserId, cancellationToken);
            await PublishInvalidationAsync(entity, "project.member.removed", cancellationToken);
        }
    }

    private ProjectManagementAccessPolicy AccessPolicy => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);

    private async Task EnsureProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var exists = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (!exists) throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementProjectMemberEntity> GetRequiredAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var entity = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.Id == id && item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken);
        return entity.FirstOrDefault() ?? throw new NotFoundException("项目成员不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task EnsureOwnerWillRemainAsync(ProjectManagementProjectMemberEntity entity, string? nextRole, CancellationToken cancellationToken)
    {
        if (!string.Equals(entity.RoleCode, "Owner", StringComparison.Ordinal) || string.Equals(nextRole, "Owner", StringComparison.Ordinal)) return;
        var ownerCount = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == entity.ProjectId && item.TenantId == entity.TenantId && item.AppCode == entity.AppCode && item.RoleCode == "Owner" && item.IsActive && !item.IsDeleted && item.Id != entity.Id)
            .CountAsync(cancellationToken);
        if (ownerCount == 0) throw new ValidationException("项目至少需要保留一个有效 Owner");
    }

    private async Task<string?> NormalizeScopeRootTaskIdAsync(string projectId, string roleCode, string? scopeRootTaskId, CancellationToken cancellationToken)
    {
        var normalizedScopeRootTaskId = NormalizeOptional(scopeRootTaskId);
        if (normalizedScopeRootTaskId is null) return null;
        if (!string.Equals(roleCode, "Lead", StringComparison.Ordinal))
            throw new ValidationException("只有 Lead 可以设置主题根任务范围");
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var isProjectRootTask = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(task => task.Id == normalizedScopeRootTaskId && task.ProjectId == projectId && task.TenantId == tenantId && task.AppCode == appCode && task.ParentTaskId == null && !task.IsDeleted)
            .AnyAsync(cancellationToken);
        if (!isProjectRootTask) throw new ValidationException("Lead 的主题根任务必须是当前项目的有效根任务");
        return normalizedScopeRootTaskId;
    }

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
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

    private async Task WriteActivityAsync(ProjectManagementProjectMemberEntity entity, string activityType, string summary, IReadOnlyList<ProjectManagementActivityFieldChange> changes, DateTime occurredAt, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            RequireTenantId(), RequireAppCode(), "ProjectMember", entity.Id, activityType, summary,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), entity.ProjectId,
            Source: "User", FieldChanges: changes, OccurredAt: occurredAt), cancellationToken);
    }

    private static IReadOnlyList<ProjectManagementActivityFieldChange> CreateChanges(MemberActivitySnapshot? before, ProjectManagementProjectMemberEntity after) =>
        ProjectManagementActivityChanges.Collect(
            ProjectManagementActivityChanges.Create("UserId", "成员用户", before?.UserId, after.UserId),
            ProjectManagementActivityChanges.Create("EmploymentId", "任职", before?.EmploymentId, after.EmploymentId),
            ProjectManagementActivityChanges.Create("RoleCode", "项目角色", before?.RoleCode, after.RoleCode),
            ProjectManagementActivityChanges.Create("ScopeRootTaskId", "主题根任务范围", before?.ScopeRootTaskId, after.ScopeRootTaskId),
            ProjectManagementActivityChanges.Create("IsActive", "有效状态", before?.IsActive, after.IsActive),
            ProjectManagementActivityChanges.Create("LeftAt", "离开时间", before?.LeftAt, after.LeftAt),
            ProjectManagementActivityChanges.Create("IsDeleted", "已删除", before?.IsDeleted, after.IsDeleted));

    private sealed record MemberActivitySnapshot(string UserId, string? EmploymentId, string RoleCode, string? ScopeRootTaskId, bool IsActive, DateTime? LeftAt, bool IsDeleted)
    {
        public static MemberActivitySnapshot From(ProjectManagementProjectMemberEntity entity) => new(entity.UserId, entity.EmploymentId, entity.RoleCode, entity.ScopeRootTaskId, entity.IsActive, entity.LeftAt, entity.IsDeleted);
    }
    private static ProjectManagementMemberResponse Map(ProjectManagementProjectMemberEntity entity) => new(entity.Id, entity.ProjectId, entity.UserId, entity.EmploymentId, entity.RoleCode, entity.ScopeRootTaskId, entity.IsActive, entity.JoinedAt, entity.LeftAt, entity.VersionNo);
}

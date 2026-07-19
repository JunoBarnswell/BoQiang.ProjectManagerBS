using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Diagnostics;
using System.Text.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementRecycleService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementTaskProgressProjector? progressProjector = null,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementSyncJournalWriter? syncJournalWriter = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null,
    ProjectManagementTaskHierarchy? taskHierarchy = null,
    IProjectManagementImConversationService? imConversationService = null,
    IProjectManagementRiskConfirmationService? riskConfirmation = null,
    IProjectManagementMaintenanceLock? maintenanceLock = null,
    IProjectManagementOperationWriter? operationWriter = null) : IProjectManagementRecycleService, ITransientDependency
{
    public async Task<ProjectManagementRecycleResponse> QueryAsync(ProjectManagementRecycleQuery query, CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        RequireTenantId();
        RequireAppCode();
        // 租户、应用和项目成员边界由已注册的 ORM Data Filter 统一生成数据库谓词。
        var projects = db.Queryable<ProjectManagementProjectEntity>().Where(item => item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.ProjectId)) projects = projects.Where(item => item.Id == query.ProjectId);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            projects = projects.Where(item => item.ProjectCode.Contains(keyword) || item.ProjectName.Contains(keyword));
        }
        var tasks = db.Queryable<ProjectManagementTaskEntity>().Where(item => item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.ProjectId)) tasks = tasks.Where(item => item.ProjectId == query.ProjectId);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            tasks = tasks.Where(item => item.TaskCode.Contains(keyword) || item.Title.Contains(keyword));
        }
        var projectTotal = new RefAsync<int>();
        var taskTotal = new RefAsync<int>();
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var projectRows = await projects.OrderBy(item => item.DeletedTime, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, projectTotal, cancellationToken);
        var taskRows = await tasks.OrderBy(item => item.DeletedTime, OrderByType.Desc).ToPageListAsync(pageIndex, pageSize, taskTotal, cancellationToken);
        var manageableProjectIds = await GetManageableProjectIdsAsync(projectRows, taskRows, cancellationToken);
        return new ProjectManagementRecycleResponse(
            new GridPageResult<ProjectManagementRecycleProjectItem> { Total = projectTotal.Value, Items = projectRows.Select(item => new ProjectManagementRecycleProjectItem(item.Id, item.ProjectCode, item.ProjectName, item.Status, item.VersionNo, item.DeletedTime, item.DeletedBy, manageableProjectIds.Contains(item.Id), manageableProjectIds.Contains(item.Id) && currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementProjectPurge))).ToList() },
            new GridPageResult<ProjectManagementRecycleTaskItem> { Total = taskTotal.Value, Items = taskRows.Select(item => new ProjectManagementRecycleTaskItem(item.Id, item.ProjectId, item.TaskCode, item.Title, item.Status, item.VersionNo, item.DeletedTime, item.DeletedBy, manageableProjectIds.Contains(item.ProjectId), false)).ToList() });
    }

    public async Task RestoreProjectAsync(string id, ProjectManagementRecycleRestoreRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetDeletedProjectAsync(id, cancellationToken);
        await EnsureCanManageProjectAsync(entity.Id, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var db = databaseAccessor.GetProjectManagementDb();
        var now = DateTime.UtcNow;
        entity.IsDeleted = false; entity.DeletedBy = null; entity.DeletedTime = null; entity.VersionNo++;
        entity.UpdatedBy = RequireUserId(); entity.UpdatedTime = now;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await RefreshProgressAsync(entity.Id, cancellationToken);
            await WriteActivityAsync("Project", entity.Id, entity.Id, "restored", $"恢复项目 {entity.ProjectName}", cancellationToken);
            await WriteSyncJournalAsync("Project", entity.Id, entity.Id, "restored", entity.VersionNo, entity, cancellationToken);
        });
        if (imConversationService is not null)
        {
            await imConversationService.ReactivateProjectLinksAsync(entity.Id, cancellationToken);
        }
        await PublishInvalidationAsync("Project", entity.Id, entity.Id, "project.restored", entity.VersionNo, cancellationToken);
    }

    public async Task RestoreTaskAsync(string id, ProjectManagementRecycleRestoreRequest request, CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        var entity = (await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == id && item.IsDeleted && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode()).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("已删除任务不存在", ErrorCodes.PlatformResourceNotFound);
        await EnsureCanManageProjectAsync(entity.ProjectId, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        if (!await db.Queryable<ProjectManagementProjectEntity>().AnyAsync(item => item.Id == entity.ProjectId && !item.IsDeleted && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode(), cancellationToken))
            throw new ValidationException("所属项目已删除，必须先恢复项目");
        var subtree = await (taskHierarchy ?? new ProjectManagementTaskHierarchy()).LoadSubtreeAsync(db, entity.ProjectId, entity.Id, cancellationToken);
        var targets = (request.RestoreDescendants ? subtree : [entity]).Where(item => item.IsDeleted).ToList();
        var targetIds = targets.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var task in targets)
        {
            if (await db.Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.ProjectId == task.ProjectId && item.TaskCode == task.TaskCode && !item.IsDeleted && !targetIds.Contains(item.Id), cancellationToken))
                throw new ValidationException($"任务编码 {task.TaskCode} 已被其他任务占用，不能恢复");
            if (task.ParentTaskId is not null && !targetIds.Contains(task.ParentTaskId) && !await db.Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.Id == task.ParentTaskId && item.ProjectId == task.ProjectId && !item.IsDeleted, cancellationToken))
                throw new ValidationException("父任务已删除或不存在，必须选择恢复完整子树或先恢复父任务");
            if (task.MilestoneId is not null && !await db.Queryable<ProjectManagementMilestoneEntity>().AnyAsync(item => item.Id == task.MilestoneId && item.ProjectId == task.ProjectId && !item.IsDeleted, cancellationToken))
                throw new ValidationException("里程碑已删除或不存在，不能恢复");
        }
        var now = DateTime.UtcNow;
        var userId = RequireUserId();
        foreach (var task in targets)
        {
            task.IsDeleted = false;
            task.DeletedBy = null;
            task.DeletedTime = null;
            task.VersionNo++;
            task.UpdatedBy = userId;
            task.UpdatedTime = now;
        }
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(targets)
                .UpdateColumns(item => new { item.IsDeleted, item.DeletedBy, item.DeletedTime, item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
            await RefreshProgressAsync(entity.ProjectId, cancellationToken);
            await WriteActivityAsync("Task", entity.Id, entity.ProjectId, "restored", targets.Count == 1 ? $"恢复任务 {entity.Title}" : $"恢复任务树 {entity.Title}（共 {targets.Count} 项）", cancellationToken);
            foreach (var task in targets)
                await WriteSyncJournalAsync("Task", task.Id, task.ProjectId, "restored", task.VersionNo, task, cancellationToken);
        });
        if (imConversationService is not null)
        {
            await imConversationService.ReactivateTaskLinksAsync(targetIds, cancellationToken);
        }
        await PublishInvalidationAsync("Task", entity.Id, entity.ProjectId, targets.Count == 1 ? "task.restored" : "task.subtree-restored", entity.VersionNo, cancellationToken);
    }

    public async Task<ProjectManagementRecyclePurgePreviewResponse> PreviewPurgeProjectAsync(string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var entity = await GetDeletedProjectAsync(id, cancellationToken);
        await EnsureCanManageProjectAsync(entity.Id, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        var references = await GetPurgeReferencesAsync(id, cancellationToken);
        var blockingReason = references.HasReferences ? "项目仍存在关联记录，不能永久删除" : null;
        return new ProjectManagementRecyclePurgePreviewResponse(entity.Id, entity.ProjectCode, entity.ProjectName, entity.VersionNo, references.MemberCount, references.MilestoneCount, references.TaskCount,
            blockingReason is null, blockingReason, "永久删除不提供单项目回滚；仅可由具备备份恢复权限的人员恢复整个数据空间备份。");
    }

    public async Task PurgeProjectAsync(string id, ProjectManagementRecyclePurgeRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewPurgeProjectAsync(id, request.VersionNo, cancellationToken);
        if (!preview.CanExecute) throw new ValidationException(preview.BlockingReason!);
        if (riskConfirmation is null || maintenanceLock is null) throw new ValidationException("高风险操作服务未配置");
        await riskConfirmation.EnsureConfirmedAsync(request.CurrentPassword, request.ConfirmRisk, cancellationToken);
        var operationId = await maintenanceLock.AcquireAsync("project-management-purge", TimeSpan.FromMinutes(5), cancellationToken);
        var started = false;
        try
        {
            var confirmation = await PreviewPurgeProjectAsync(id, request.VersionNo, cancellationToken);
            if (!confirmation.CanExecute) throw new ValidationException(confirmation.BlockingReason!);
            if (operationWriter is not null)
            {
                await operationWriter.StartAsync(operationId, "project.purge", JsonSerializer.Serialize(confirmation), Activity.Current?.Id ?? operationId, cancellationToken);
                started = true;
            }
            var db = databaseAccessor.GetProjectManagementDb();
            var affected = await DeleteUnreferencedProjectAsync(db, id, request.VersionNo, cancellationToken);
            if (affected != 1) throw new ValidationException("对象已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
            if (operationWriter is not null) await operationWriter.SucceedAsync(operationId, cancellationToken);
        }
        catch (Exception exception)
        {
            if (started && operationWriter is not null) { try { await operationWriter.FailAsync(operationId, exception.Message, CancellationToken.None); } catch { } }
            throw;
        }
        finally { await maintenanceLock.ReleaseAsync(operationId, CancellationToken.None); }
        await PublishInvalidationAsync("Project", preview.ProjectId, preview.ProjectId, "project.purged", preview.VersionNo, cancellationToken);
    }

    private async Task<ProjectPurgeReferences> GetPurgeReferencesAsync(string projectId, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var members = await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode).CountAsync(cancellationToken);
        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode).CountAsync(cancellationToken);
        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode).CountAsync(cancellationToken);
        if (members + milestones + tasks > 0) return new ProjectPurgeReferences(members, milestones, tasks, true);

        var hasAdditionalReferences =
            await db.Queryable<ProjectManagementActivityEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementImConversationLinkEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementLabelEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementTaskLabelEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementTaskTimeLogEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementTaskTemplateEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementTaskOccurrenceEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementTaskCommentEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementNotificationEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementTaskReminderEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementSavedViewEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementTaskAttachmentEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementTaskDependencyEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementTaskParticipantEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken) ||
            await db.Queryable<ProjectManagementSyncJournalEntity>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode, cancellationToken);
        return new ProjectPurgeReferences(members, milestones, tasks, hasAdditionalReferences);
    }

    private Task<int> DeleteUnreferencedProjectAsync(ISqlSugarClient db, string projectId, long versionNo, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        return db.Deleteable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.IsDeleted && item.VersionNo == versionNo && item.TenantId == tenantId && item.AppCode == appCode)
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementMilestoneEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementActivityEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementImConversationLinkEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementLabelEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementTaskLabelEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementTaskTimeLogEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementTaskTemplateEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementTaskOccurrenceEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementTaskCommentEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementNotificationEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementTaskReminderEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementSavedViewEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementTaskAttachmentEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementTaskDependencyEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementTaskParticipantEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .Where(item => !SqlFunc.Subqueryable<ProjectManagementSyncJournalEntity>().Where(reference => reference.ProjectId == projectId && reference.TenantId == tenantId && reference.AppCode == appCode).Any())
            .ExecuteCommandAsync(cancellationToken);
    }

    private sealed record ProjectPurgeReferences(int MemberCount, int MilestoneCount, int TaskCount, bool HasReferences);

    private async Task<ProjectManagementProjectEntity> GetDeletedProjectAsync(string id, CancellationToken cancellationToken) => (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == id && item.IsDeleted && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode()).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("已删除项目不存在", ErrorCodes.PlatformResourceNotFound);

    private async Task<HashSet<string>> GetManageableProjectIdsAsync(
        IReadOnlyCollection<ProjectManagementProjectEntity> projects,
        IReadOnlyCollection<ProjectManagementTaskEntity> tasks,
        CancellationToken cancellationToken)
    {
        var projectIds = projects.Select(item => item.Id)
            .Concat(tasks.Select(item => item.ProjectId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (projectIds.Count == 0) return [];
        if (currentUser.IsAsterErpPlatformAdmin() || currentUser.HasAsterErpPermission("*"))
            return projectIds.ToHashSet(StringComparer.Ordinal);

        var userId = RequireUserId();
        var ownedProjectIds = projects
            .Where(item => string.Equals(item.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var memberships = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => projectIds.Contains(item.ProjectId) && item.UserId == userId && item.IsActive && !item.IsDeleted &&
                (item.RoleCode == "Owner" || item.RoleCode == "Manager"))
            .ToListAsync(cancellationToken);
        ownedProjectIds.UnionWith(memberships.Select(item => item.ProjectId));
        return ownedProjectIds;
    }
    private Task EnsureCanManageProjectAsync(string projectId, CancellationToken cancellationToken) =>
        (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageDeletedProjectAsync(projectId, cancellationToken);

    private Task RefreshProgressAsync(string projectId, CancellationToken cancellationToken) =>
        (progressProjector ?? new ProjectManagementTaskProgressProjector(databaseAccessor)).RefreshAsync(projectId, cancellationToken);

    private async Task WriteActivityAsync(string aggregateType, string aggregateId, string projectId, string activityType, string summary, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(RequireTenantId(), RequireAppCode(), aggregateType, aggregateId, activityType, summary, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), projectId), cancellationToken);
    }

    private async Task WriteSyncJournalAsync(string aggregateType, string aggregateId, string projectId, string operation, long versionNo, object snapshot, CancellationToken cancellationToken)
    {
        if (syncJournalWriter is null) return;
        await syncJournalWriter.AppendAsync(new ProjectManagementSyncJournalEvent(RequireTenantId(), RequireAppCode(), aggregateType, aggregateId, projectId, operation, versionNo, JsonSerializer.Serialize(snapshot), RequireUserId(), null, Activity.Current?.Id ?? Guid.NewGuid().ToString("N")), cancellationToken);
    }

    private async Task PublishInvalidationAsync(string aggregateType, string aggregateId, string projectId, string eventType, long versionNo, CancellationToken cancellationToken)
    {
        if (realtimePublisher is null) return;
        await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(RequireTenantId(), RequireAppCode(), aggregateType, aggregateId, eventType, versionNo, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), projectId), cancellationToken);
    }
    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private static string RequireAppCode() => ProjectManagementPlatformScope.AppCode;
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static void EnsureVersion(long actual, long expected) { if (expected <= 0 || actual != expected) throw new ValidationException("对象已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
}

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
    IProjectManagementOperationWriter? operationWriter = null,
    IProjectManagementTaskDependencyService? dependencyService = null) : IProjectManagementRecycleService, ITransientDependency
{
    public async Task<ProjectManagementRecycleResponse> QueryAsync(ProjectManagementRecycleQuery query, CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetCurrentDb();
        RequirePlatformScope();
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
        var impact = await GetImpactAsync(projectRows, taskRows, cancellationToken);
        var manageableProjectIds = await GetManageableProjectIdsAsync(projectRows, taskRows, impact.ProjectsById.Values.ToList(), cancellationToken);
        return new ProjectManagementRecycleResponse(
            new GridPageResult<ProjectManagementRecycleProjectItem> { Total = projectTotal.Value, Items = projectRows.Select(item => new ProjectManagementRecycleProjectItem(item.Id, item.ProjectCode, item.ProjectName, item.Status, item.VersionNo, item.DeletedTime, item.DeletedBy, impact.TaskCountByProjectId.GetValueOrDefault(item.Id), manageableProjectIds.Contains(item.Id), manageableProjectIds.Contains(item.Id) && currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementProjectPurge))).ToList() },
            new GridPageResult<ProjectManagementRecycleTaskItem> { Total = taskTotal.Value, Items = taskRows.Select(item => new ProjectManagementRecycleTaskItem(item.Id, item.ProjectId, item.TaskCode, item.Title, item.Status, item.VersionNo, item.DeletedTime, item.DeletedBy, impact.DescendantCountByTaskId.GetValueOrDefault(item.Id), manageableProjectIds.Contains(item.ProjectId), manageableProjectIds.Contains(item.ProjectId) && currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementTaskDelete))).ToList() });
    }

    public async Task RestoreProjectAsync(string id, ProjectManagementRecycleRestoreRequest request, CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetDeletedProjectAsync(id, cancellationToken);
        await EnsureCanManageProjectAsync(entity.Id, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var db = databaseAccessor.GetCurrentDb();
        if (await db.Queryable<ProjectManagementProjectEntity>().AnyAsync(item => item.Id != entity.Id && item.ProjectCode == entity.ProjectCode && !item.IsDeleted, cancellationToken))
            throw new ValidationException($"项目编码 {entity.ProjectCode} 已被其他项目占用，不能恢复");
        var now = DateTime.UtcNow;
        entity.IsDeleted = false; entity.DeletedBy = null; entity.DeletedTime = null; entity.VersionNo++;
        entity.UpdatedBy = RequireUserId(); entity.UpdatedTime = now;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await RefreshProgressAsync(entity.Id, cancellationToken);
            await RefreshDependencyStatesAsync(entity.Id, cancellationToken);
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
        var db = databaseAccessor.GetCurrentDb();
        RequirePlatformScope();
        var entity = (await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == id && item.IsDeleted && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode()).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("已删除任务不存在", ErrorCodes.PlatformResourceNotFound);
        await EnsureCanManageProjectAsync(entity.ProjectId, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        if (!await db.Queryable<ProjectManagementProjectEntity>().AnyAsync(item => item.Id == entity.ProjectId && !item.IsDeleted && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode(), cancellationToken))
            throw new ValidationException("所属项目已删除，必须先恢复项目");
        var subtree = await (taskHierarchy ?? new ProjectManagementTaskHierarchy()).LoadSubtreeAsync(db, entity.ProjectId, entity.Id, cancellationToken);
        var targets = (request.RestoreDescendants ? subtree : [entity]).Where(item => item.IsDeleted).ToList();
        if (targets.Count == 0) throw new ValidationException("没有可恢复的任务");
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
        await EnsureWipCapacityAsync(entity.ProjectId, targets, cancellationToken);
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
            await RefreshDependencyStatesAsync(entity.ProjectId, cancellationToken);
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

    public async Task<ProjectManagementRecycleTaskPurgePreviewResponse> PreviewPurgeTaskAsync(string id, long versionNo, bool purgeDescendants = false, CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetDeletedTaskAsync(id, cancellationToken);
        await EnsureCanManageProjectAsync(entity.ProjectId, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        var targets = await LoadPurgeTaskTargetsAsync(entity, purgeDescendants, cancellationToken);
        var taskIds = targets.Select(item => item.Id).ToList();
        var db = databaseAccessor.GetCurrentDb();
        var dependencyCount = await db.Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => item.ProjectId == entity.ProjectId && (taskIds.Contains(item.PredecessorTaskId) || taskIds.Contains(item.SuccessorTaskId)))
            .CountAsync(cancellationToken);
        var blockingReason = await GetTaskPurgeBlockingReasonAsync(entity.ProjectId, taskIds, cancellationToken);
        return new ProjectManagementRecycleTaskPurgePreviewResponse(entity.Id, entity.ProjectId, entity.TaskCode, entity.Title, entity.VersionNo, taskIds.Count, dependencyCount,
            blockingReason is null, blockingReason,
            "任务永久删除不可恢复。任务活动会作为项目级审计保留，直至项目自身永久删除时统一清理；其他任务业务关联必须先单独处理。");
    }

    public async Task PurgeTaskAsync(string id, ProjectManagementRecycleTaskPurgeRequest request, CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var preview = await PreviewPurgeTaskAsync(id, request.VersionNo, request.PurgeDescendants, cancellationToken);
        if (!preview.CanExecute) throw new ValidationException(preview.BlockingReason!);
        if (riskConfirmation is null || maintenanceLock is null) throw new ValidationException("高风险操作服务未配置");
        await riskConfirmation.EnsureConfirmedAsync(request.CurrentPassword, request.ConfirmRisk, cancellationToken);
        var operationId = await maintenanceLock.AcquireAsync("project-management-task-purge", TimeSpan.FromMinutes(5), cancellationToken);
        var started = false;
        try
        {
            var confirmation = await PreviewPurgeTaskAsync(id, request.VersionNo, request.PurgeDescendants, cancellationToken);
            if (!confirmation.CanExecute) throw new ValidationException(confirmation.BlockingReason!);
            if (operationWriter is not null)
            {
                await operationWriter.StartAsync(operationId, "task.purge", JsonSerializer.Serialize(confirmation), Activity.Current?.Id ?? operationId, cancellationToken);
                started = true;
            }
            var entity = await GetDeletedTaskAsync(id, cancellationToken);
            var targets = await LoadPurgeTaskTargetsAsync(entity, request.PurgeDescendants, cancellationToken);
            var affected = await DependencyService.PurgeDeletedTasksAsync(entity.ProjectId, targets.Select(item => item.Id).ToList(), cancellationToken);
            if (affected != targets.Count) throw new ValidationException("任务永久删除过程中检测到并发变更，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
            if (operationWriter is not null) await operationWriter.SucceedAsync(operationId, cancellationToken);
        }
        catch (Exception exception)
        {
            if (started && operationWriter is not null) { try { await operationWriter.FailAsync(operationId, exception.Message, CancellationToken.None); } catch { } }
            throw;
        }
        finally { await maintenanceLock.ReleaseAsync(operationId, CancellationToken.None); }
        await PublishInvalidationAsync("Task", preview.TaskId, preview.ProjectId, "task.purged", preview.VersionNo, cancellationToken);
    }

    public async Task<ProjectManagementRecyclePurgePreviewResponse> PreviewPurgeProjectAsync(string id, long versionNo, CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
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
        RequirePlatformScope();
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
            var db = databaseAccessor.GetCurrentDb();
            var affected = 0;
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                // 项目永久删除是审计保留的最终边界：删除全部项目活动，避免 task.purged 审计反向阻塞项目 purge。
                await db.Deleteable<ProjectManagementActivityEntity>().Where(item => item.ProjectId == id).ExecuteCommandAsync(cancellationToken);
                affected = await DeleteUnreferencedProjectAsync(db, id, request.VersionNo, cancellationToken);
            });
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
        var db = databaseAccessor.GetCurrentDb();
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var members = await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode).CountAsync(cancellationToken);
        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode).CountAsync(cancellationToken);
        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode).CountAsync(cancellationToken);
        if (members + milestones + tasks > 0) return new ProjectPurgeReferences(members, milestones, tasks, true);

        var hasAdditionalReferences =
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

    private async Task<ProjectManagementProjectEntity> GetDeletedProjectAsync(string id, CancellationToken cancellationToken) => (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == id && item.IsDeleted && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode()).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("已删除项目不存在", ErrorCodes.PlatformResourceNotFound);

    private async Task<ProjectManagementTaskEntity> GetDeletedTaskAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.Id == id && item.IsDeleted && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode())
            .Take(1)
            .ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("已删除任务不存在", ErrorCodes.PlatformResourceNotFound);

    private async Task<IReadOnlyList<ProjectManagementTaskEntity>> LoadPurgeTaskTargetsAsync(ProjectManagementTaskEntity root, bool purgeDescendants, CancellationToken cancellationToken)
    {
        var subtree = await (taskHierarchy ?? new ProjectManagementTaskHierarchy()).LoadSubtreeAsync(databaseAccessor.GetCurrentDb(), root.ProjectId, root.Id, cancellationToken);
        if (!purgeDescendants && subtree.Any(item => item.Id != root.Id))
            throw new ValidationException("任务仍包含子任务，必须选择永久删除完整子树");
        var targets = purgeDescendants ? subtree : [root];
        if (targets.Any(item => !item.IsDeleted)) throw new ValidationException("任务子树包含未删除任务，不能永久删除");
        return targets;
    }

    private async Task<string?> GetTaskPurgeBlockingReasonAsync(string projectId, IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        // 活动记录刻意不作为任务 purge 的阻塞项：它们是不可变项目审计，会保留到项目永久删除的最终边界。
        var hasOperationalReferences =
            await db.Queryable<ProjectManagementTaskParticipantEntity>().AnyAsync(item => item.ProjectId == projectId && taskIds.Contains(item.TaskId), cancellationToken) ||
            await db.Queryable<ProjectManagementTaskLabelEntity>().AnyAsync(item => item.ProjectId == projectId && taskIds.Contains(item.TaskId), cancellationToken) ||
            await db.Queryable<ProjectManagementTaskTimeLogEntity>().AnyAsync(item => item.ProjectId == projectId && taskIds.Contains(item.TaskId), cancellationToken) ||
            await db.Queryable<ProjectManagementTaskCommentEntity>().AnyAsync(item => item.ProjectId == projectId && taskIds.Contains(item.TaskId), cancellationToken) ||
            await db.Queryable<ProjectManagementTaskAttachmentEntity>().AnyAsync(item => item.ProjectId == projectId && taskIds.Contains(item.TaskId), cancellationToken) ||
            await db.Queryable<ProjectManagementNotificationEntity>().AnyAsync(item => item.ProjectId == projectId && taskIds.Contains(item.TaskId), cancellationToken) ||
            await db.Queryable<ProjectManagementTaskReminderEntity>().AnyAsync(item => item.ProjectId == projectId && taskIds.Contains(item.TaskId), cancellationToken) ||
            await db.Queryable<ProjectManagementTaskOccurrenceEntity>().AnyAsync(item => item.ProjectId == projectId && taskIds.Contains(item.RootTaskId), cancellationToken);
        return hasOperationalReferences ? "任务仍存在参与人、标签、工时、评论、附件、通知、提醒或排期等业务关联，必须先处理这些关联后才能永久删除" : null;
    }

    private async Task<HashSet<string>> GetManageableProjectIdsAsync(
        IReadOnlyCollection<ProjectManagementProjectEntity> projects,
        IReadOnlyCollection<ProjectManagementTaskEntity> tasks,
        IReadOnlyCollection<ProjectManagementProjectEntity> taskProjects,
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
        var ownedProjectIds = projects.Concat(taskProjects)
            .Where(item => string.Equals(item.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var memberships = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
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

    private Task RefreshDependencyStatesAsync(string projectId, CancellationToken cancellationToken) =>
        (dependencyService ?? new ProjectManagementTaskDependencyService(databaseAccessor, currentUser)).RefreshBlockedStatesAsync(projectId, cancellationToken);

    private IProjectManagementTaskDependencyService DependencyService => dependencyService ?? new ProjectManagementTaskDependencyService(databaseAccessor, currentUser, activityWriter: activityWriter);

    private async Task EnsureWipCapacityAsync(string projectId, IReadOnlyCollection<ProjectManagementTaskEntity> targets, CancellationToken cancellationToken)
    {
        var project = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new ValidationException("所属项目不存在或已删除，不能恢复任务");
        if (!project.WipLimit.HasValue) return;
        var restoringInProgressCount = targets.Count(item => item.Status == ProjectManagementDomainRules.TaskInProgress);
        if (restoringInProgressCount == 0) return;
        var targetIds = targets.Select(item => item.Id).ToList();
        var currentInProgressCount = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted && item.Status == ProjectManagementDomainRules.TaskInProgress && !targetIds.Contains(item.Id))
            .CountAsync(cancellationToken);
        if (currentInProgressCount + restoringInProgressCount > project.WipLimit.Value)
            throw new ValidationException($"恢复后进行中任务数将达到 {currentInProgressCount + restoringInProgressCount}，超过项目 WIP 上限 {project.WipLimit.Value}");
    }

    private async Task<RecycleImpact> GetImpactAsync(
        IReadOnlyCollection<ProjectManagementProjectEntity> projects,
        IReadOnlyCollection<ProjectManagementTaskEntity> tasks,
        CancellationToken cancellationToken)
    {
        var projectIds = projects.Select(item => item.Id).Concat(tasks.Select(item => item.ProjectId)).Distinct(StringComparer.Ordinal).ToList();
        if (projectIds.Count == 0) return RecycleImpact.Empty;
        var db = databaseAccessor.GetCurrentDb();
        var taskRows = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => projectIds.Contains(item.ProjectId))
            .ToListAsync(cancellationToken);
        var taskCountByProjectId = taskRows.GroupBy(item => item.ProjectId)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var childrenByParentId = taskRows.Where(item => !string.IsNullOrWhiteSpace(item.ParentTaskId))
            .GroupBy(item => item.ParentTaskId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList(), StringComparer.Ordinal);
        var descendantCountByTaskId = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var task in tasks)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>([task.Id]);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current)) continue;
                if (childrenByParentId.TryGetValue(current, out var children))
                    foreach (var child in children) queue.Enqueue(child);
            }
            descendantCountByTaskId[task.Id] = Math.Max(0, visited.Count - 1);
        }
        var taskProjects = taskRows.Where(item => projectIds.Contains(item.ProjectId)).Select(item => item.ProjectId).Distinct(StringComparer.Ordinal).ToList();
        var projectsById = taskProjects.Count == 0
            ? new Dictionary<string, ProjectManagementProjectEntity>(StringComparer.Ordinal)
            : (await db.Queryable<ProjectManagementProjectEntity>().Where(item => taskProjects.Contains(item.Id)).ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id, StringComparer.Ordinal);
        return new RecycleImpact(taskCountByProjectId, descendantCountByTaskId, projectsById);
    }

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
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private void RequirePlatformScope() => ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
    private static void EnsureVersion(long actual, long expected) { if (expected <= 0 || actual != expected) throw new ValidationException("对象已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private sealed record RecycleImpact(
        IReadOnlyDictionary<string, int> TaskCountByProjectId,
        IReadOnlyDictionary<string, int> DescendantCountByTaskId,
        IReadOnlyDictionary<string, ProjectManagementProjectEntity> ProjectsById)
    {
        public static RecycleImpact Empty { get; } = new(
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<string, ProjectManagementProjectEntity>(StringComparer.Ordinal));
    }
}

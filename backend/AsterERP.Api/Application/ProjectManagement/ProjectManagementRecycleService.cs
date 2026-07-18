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
    IProjectManagementTaskDependencyService? dependencyService = null,
    IProjectManagementFileStore? fileStore = null,
    IProjectManagementReversibleCommandWriter? reversibleCommandWriter = null) : IProjectManagementRecycleService, ITransientDependency
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
        var userId = RequireUserId();
        var isAdministrator = currentUser.IsAsterErpPlatformAdmin() || currentUser.HasAsterErpPermission("*");
        var canPurgeProject = (ProjectManagementProjectEntity item) => (isAdministrator || string.Equals(item.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase)) && currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementProjectPurge);
        var canPurgeTask = (ProjectManagementTaskEntity item) => impact.ProjectsById.TryGetValue(item.ProjectId, out var project) &&
            (isAdministrator || string.Equals(project.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase)) && currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementTaskPurge);
        return new ProjectManagementRecycleResponse(
            new GridPageResult<ProjectManagementRecycleProjectItem> { Total = projectTotal.Value, Items = projectRows.Select(item => new ProjectManagementRecycleProjectItem(item.Id, item.ProjectCode, item.ProjectName, item.Status, item.VersionNo, item.DeletedTime, item.DeletedBy, impact.TaskCountByProjectId.GetValueOrDefault(item.Id), manageableProjectIds.Contains(item.Id), canPurgeProject(item))).ToList() },
            new GridPageResult<ProjectManagementRecycleTaskItem> { Total = taskTotal.Value, Items = taskRows.Select(item => new ProjectManagementRecycleTaskItem(item.Id, item.ProjectId, item.TaskCode, item.Title, item.Status, item.VersionNo, item.DeletedTime, item.DeletedBy, impact.DescendantCountByTaskId.GetValueOrDefault(item.Id), manageableProjectIds.Contains(item.ProjectId), canPurgeTask(item))).ToList() });
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
            await WriteActivityAsync("Project", entity.Id, entity.Id, "restored", $"恢复项目 {entity.ProjectName}",
                ProjectManagementActivityChanges.Collect(ProjectManagementActivityChanges.Create("IsDeleted", "已删除", true, false)), now, null, cancellationToken);
            await WriteSyncJournalAsync("Project", entity.Id, entity.Id, "restored", entity.VersionNo, entity, cancellationToken);
        });
        if (imConversationService is not null)
        {
            await imConversationService.ReactivateProjectLinksAsync(entity.Id, cancellationToken);
        }
        await PublishInvalidationAsync("Project", entity.Id, entity.Id, "project.restored", entity.VersionNo, cancellationToken);
        await RecordReversibleAsync(ProjectManagementReversibleCommandTypes.ProjectRestored, entity.Id, "Project", entity.Id,
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementProjectRestoreCommand(entity.Id, request.VersionNo)),
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementProjectDeleteCommand(entity.Id, entity.VersionNo)),
            $"恢复项目 {entity.ProjectName}", cancellationToken);
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
            await WriteActivityAsync("Task", entity.Id, entity.ProjectId, "restored", targets.Count == 1 ? $"恢复任务 {entity.Title}" : $"恢复任务树 {entity.Title}（共 {targets.Count} 项）",
                ProjectManagementActivityChanges.Collect(ProjectManagementActivityChanges.Create("IsDeleted", "已删除", true, false)),
                now,
                targets.Count <= 1 ? null : new ProjectManagementActivityBatch($"restore:{entity.Id}:{now:O}", targets.Count, targets.Count, 0,
                    targets.Select(task => new ProjectManagementActivityBatchItem("Task", task.Id, $"恢复任务 {task.Title}",
                        ProjectManagementActivityChanges.Collect(ProjectManagementActivityChanges.Create("IsDeleted", "已删除", true, false)))).ToList()),
                cancellationToken);
            foreach (var task in targets)
                await WriteSyncJournalAsync("Task", task.Id, task.ProjectId, "restored", task.VersionNo, task, cancellationToken);
        });
        if (imConversationService is not null)
        {
            await imConversationService.ReactivateTaskLinksAsync(targetIds, cancellationToken);
        }
        await PublishInvalidationAsync("Task", entity.Id, entity.ProjectId, targets.Count == 1 ? "task.restored" : "task.subtree-restored", entity.VersionNo, cancellationToken);
        await RecordReversibleAsync(ProjectManagementReversibleCommandTypes.TaskRestored, entity.ProjectId, "Task", entity.Id,
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementTaskRestoreCommand(entity.Id, request.RestoreDescendants, request.VersionNo)),
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementTaskDeleteCommand(entity.Id, ProjectManagementTaskDeleteModes.Cascade, entity.VersionNo)),
            targets.Count == 1 ? $"恢复任务 {entity.Title}" : $"恢复任务树 {entity.Title}", cancellationToken);
    }

    public async Task<ProjectManagementRecycleTaskPurgePreviewResponse> PreviewPurgeTaskAsync(string id, long versionNo, bool purgeDescendants = false, CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetDeletedTaskAsync(id, cancellationToken);
        await EnsureCanPurgeProjectAsync(entity.ProjectId, PermissionCodes.ProjectManagementTaskPurge, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        var targets = await LoadPurgeTaskTargetsAsync(entity, purgeDescendants, cancellationToken);
        var taskIds = targets.Select(item => item.Id).ToList();
        var db = databaseAccessor.GetCurrentDb();
        var impact = await GetPurgeImpactAsync(entity.ProjectId, taskIds, cancellationToken);
        var blockingReason = impact.AttachmentCount > 0 && fileStore is null ? "附件文件生命周期服务未配置，不能安全永久删除" : null;
        return new ProjectManagementRecycleTaskPurgePreviewResponse(entity.Id, entity.ProjectId, entity.TaskCode, entity.Title, entity.VersionNo, taskIds.Count, impact.DependencyCount, impact,
            blockingReason is null, blockingReason,
            "任务及子树的关系、评论、附件、提醒和同步记录将一并永久清理；操作不可撤销，结果会保留在不可变操作审计中。");
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
            var taskIds = targets.Select(item => item.Id).ToList();
            var attachments = await LoadAttachmentsAsync(entity.ProjectId, taskIds, cancellationToken);
            await DeleteAttachmentFilesAsync(attachments, cancellationToken);
            var db = databaseAccessor.GetCurrentDb();
            var affected = 0;
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                affected = await DeleteTaskTreeAsync(db, entity.ProjectId, taskIds, cancellationToken);
            });
            if (affected != targets.Count) throw new ValidationException("任务永久删除过程中检测到并发变更，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
            if (operationWriter is not null) await operationWriter.CompleteWithImpactAsync(operationId, JsonSerializer.Serialize(confirmation), cancellationToken);
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
        await EnsureCanPurgeProjectAsync(entity.Id, PermissionCodes.ProjectManagementProjectPurge, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        var impact = await GetPurgeImpactAsync(id, null, cancellationToken);
        var blockingReason = impact.AttachmentCount > 0 && fileStore is null ? "附件文件生命周期服务未配置，不能安全永久删除" : null;
        return new ProjectManagementRecyclePurgePreviewResponse(entity.Id, entity.ProjectCode, entity.ProjectName, entity.VersionNo, impact.MemberCount, impact.MilestoneCount, impact.TaskCount, impact,
            blockingReason is null, blockingReason, "项目、任务树及全部关联数据都会永久清理且不可撤销；操作结果将作为不可变审计保留。");
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
            var attachments = await LoadAttachmentsAsync(id, null, cancellationToken);
            await DeleteAttachmentFilesAsync(attachments, cancellationToken);
            var db = databaseAccessor.GetCurrentDb();
            var affected = 0;
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                affected = await DeleteProjectGraphAsync(db, id, request.VersionNo, cancellationToken);
            });
            if (affected != 1) throw new ValidationException("对象已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
            if (operationWriter is not null) await operationWriter.CompleteWithImpactAsync(operationId, JsonSerializer.Serialize(confirmation), cancellationToken);
        }
        catch (Exception exception)
        {
            if (started && operationWriter is not null) { try { await operationWriter.FailAsync(operationId, exception.Message, CancellationToken.None); } catch { } }
            throw;
        }
        finally { await maintenanceLock.ReleaseAsync(operationId, CancellationToken.None); }
        await PublishInvalidationAsync("Project", preview.ProjectId, preview.ProjectId, "project.purged", preview.VersionNo, cancellationToken);
    }
    private async Task<ProjectManagementRecyclePurgeImpact> GetPurgeImpactAsync(string projectId, IReadOnlyCollection<string>? taskIds, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var allProjectData = taskIds is null;
        var ids = taskIds?.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToList() ?? [];
        var taskCount = allProjectData
            ? await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken)
            : ids.Count;
        var taskScoped = ids.Count > 0;
        var memberCount = allProjectData ? await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken) : 0;
        var milestoneCount = allProjectData ? await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken) : 0;
        var dependencyCount = allProjectData
            ? await db.Queryable<ProjectManagementTaskDependencyEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken)
            : await db.Queryable<ProjectManagementTaskDependencyEntity>().Where(item => item.ProjectId == projectId && (ids.Contains(item.PredecessorTaskId) || ids.Contains(item.SuccessorTaskId))).CountAsync(cancellationToken);
        var participantCount = taskScoped ? await db.Queryable<ProjectManagementTaskParticipantEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).CountAsync(cancellationToken) : 0;
        var labelRelationCount = taskScoped ? await db.Queryable<ProjectManagementTaskLabelEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).CountAsync(cancellationToken) : 0;
        var timeLogCount = taskScoped ? await db.Queryable<ProjectManagementTaskTimeLogEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).CountAsync(cancellationToken) : 0;
        var commentCount = taskScoped ? await db.Queryable<ProjectManagementTaskCommentEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).CountAsync(cancellationToken) : 0;
        var attachmentCount = taskScoped ? await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).CountAsync(cancellationToken) : 0;
        var reminderCount = taskScoped ? await db.Queryable<ProjectManagementTaskReminderEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).CountAsync(cancellationToken) : 0;
        var notificationCount = taskScoped ? await db.Queryable<ProjectManagementNotificationEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId!)).CountAsync(cancellationToken) : 0;
        var recurrenceCount = taskScoped ? await db.Queryable<ProjectManagementTaskRecurrenceEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.SourceTaskId)).CountAsync(cancellationToken) : 0;
        var occurrenceCount = taskScoped
            ? await db.Queryable<ProjectManagementTaskRecurrenceOccurrenceEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).CountAsync(cancellationToken) + await db.Queryable<ProjectManagementTaskOccurrenceEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.RootTaskId)).CountAsync(cancellationToken)
            : 0;
        var conversationLinkCount = taskScoped ? await db.Queryable<ProjectManagementImConversationLinkEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId!)).CountAsync(cancellationToken) : 0;
        var savedViewCount = allProjectData ? await db.Queryable<ProjectManagementSavedViewEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken) : 0;
        var syncJournalCount = allProjectData ? await db.Queryable<ProjectManagementSyncJournalEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken) : await db.Queryable<ProjectManagementSyncJournalEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.AggregateId)).CountAsync(cancellationToken);
        if (allProjectData)
        {
            participantCount = await db.Queryable<ProjectManagementTaskParticipantEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken);
            labelRelationCount = await db.Queryable<ProjectManagementTaskLabelEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken);
            timeLogCount = await db.Queryable<ProjectManagementTaskTimeLogEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken);
            commentCount = await db.Queryable<ProjectManagementTaskCommentEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken);
            attachmentCount = await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken);
            reminderCount = await db.Queryable<ProjectManagementTaskReminderEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken);
            notificationCount = await db.Queryable<ProjectManagementNotificationEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken);
            recurrenceCount = await db.Queryable<ProjectManagementTaskRecurrenceEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken);
            occurrenceCount = await db.Queryable<ProjectManagementTaskRecurrenceOccurrenceEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken) + await db.Queryable<ProjectManagementTaskOccurrenceEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken);
            conversationLinkCount = await db.Queryable<ProjectManagementImConversationLinkEntity>().Where(item => item.ProjectId == projectId).CountAsync(cancellationToken);
        }
        return new ProjectManagementRecyclePurgeImpact(allProjectData ? 1 : 0, taskCount, Math.Max(0, taskCount - 1), memberCount, milestoneCount, dependencyCount, participantCount, labelRelationCount, timeLogCount, commentCount, attachmentCount, reminderCount, notificationCount, recurrenceCount, occurrenceCount, conversationLinkCount, savedViewCount, syncJournalCount);
    }

    private async Task<List<ProjectManagementTaskAttachmentEntity>> LoadAttachmentsAsync(string projectId, IReadOnlyCollection<string>? taskIds, CancellationToken cancellationToken)
    {
        var query = databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.ProjectId == projectId);
        if (taskIds is not null) query = query.Where(item => taskIds.Contains(item.TaskId));
        return await query.ToListAsync(cancellationToken);
    }

    private async Task DeleteAttachmentFilesAsync(IReadOnlyCollection<ProjectManagementTaskAttachmentEntity> attachments, CancellationToken cancellationToken)
    {
        if (attachments.Count == 0) return;
        if (fileStore is null) throw new ValidationException("附件文件生命周期服务未配置，不能安全永久删除");
        foreach (var fileId in attachments.Select(item => item.FileId).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await fileStore.DeleteAsync(fileId, cancellationToken);
        }
    }

    private async Task<int> DeleteTaskTreeAsync(ISqlSugarClient db, string projectId, IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken)
    {
        var ids = taskIds.Distinct(StringComparer.Ordinal).ToList();
        await db.Deleteable<ProjectManagementTaskCommentEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskAttachmentEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskParticipantEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskLabelEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskTimeLogEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskReminderEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementNotificationEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId!)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskRecurrenceOccurrenceEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskRecurrenceEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.SourceTaskId)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskOccurrenceEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.RootTaskId)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskDependencyEntity>().Where(item => item.ProjectId == projectId && (ids.Contains(item.PredecessorTaskId) || ids.Contains(item.SuccessorTaskId))).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementImConversationLinkEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.TaskId!)).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementSyncJournalEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.AggregateId)).ExecuteCommandAsync(cancellationToken);
        return await db.Deleteable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId && item.IsDeleted && ids.Contains(item.Id)).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<int> DeleteProjectGraphAsync(ISqlSugarClient db, string projectId, long versionNo, CancellationToken cancellationToken)
    {
        var taskIds = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId).Select(item => item.Id).ToListAsync(cancellationToken);
        if (taskIds.Count > 0) await DeleteTaskTreeAsync(db, projectId, taskIds, cancellationToken);
        await db.Deleteable<ProjectManagementTaskRecurrenceOccurrenceEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskRecurrenceEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskOccurrenceEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskTemplateEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementProjectMemberEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementMilestoneEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskLabelEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementLabelEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementNotificationEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskReminderEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskParticipantEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskTimeLogEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskCommentEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskAttachmentEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementTaskDependencyEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementImConversationLinkEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementSavedViewEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementSyncJournalEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ProjectManagementActivityEntity>().Where(item => item.ProjectId == projectId).ExecuteCommandAsync(cancellationToken);
        return await db.Deleteable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && item.IsDeleted && item.VersionNo == versionNo && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode()).ExecuteCommandAsync(cancellationToken);
    }

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

    private async Task EnsureCanPurgeProjectAsync(string projectId, string permissionCode, CancellationToken cancellationToken)
    {
        RequirePlatformScope();
        if (!currentUser.HasAsterErpPermission(permissionCode))
            throw new ValidationException("当前用户缺少永久删除权限", ErrorCodes.PermissionDenied);
        if (currentUser.IsAsterErpPlatformAdmin() || currentUser.HasAsterErpPermission("*")) return;
        var project = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode())
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault() ?? throw new NotFoundException("所属项目不存在", ErrorCodes.PlatformResourceNotFound);
        if (!string.Equals(project.OwnerUserId, RequireUserId(), StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("永久删除仅允许平台管理员或项目 Owner 执行", ErrorCodes.PermissionDenied);
    }

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

    private async Task WriteActivityAsync(
        string aggregateType,
        string aggregateId,
        string projectId,
        string activityType,
        string summary,
        IReadOnlyList<ProjectManagementActivityFieldChange> changes,
        DateTime occurredAt,
        ProjectManagementActivityBatch? batch,
        CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            RequireTenantId(), RequireAppCode(), aggregateType, aggregateId, activityType, summary,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), projectId,
            Source: "User", FieldChanges: changes, Batch: batch, OccurredAt: occurredAt), cancellationToken);
    }

    private Task RecordReversibleAsync(string commandType, string projectId, string aggregateType, string aggregateId, string forwardJson, string inverseJson, string summary, CancellationToken cancellationToken)
    {
        if (reversibleCommandWriter is null || ProjectManagementReversibleCommandReplayScope.IsActive) return Task.CompletedTask;
        var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        return reversibleCommandWriter.TryRecordCommittedAsync(ProjectManagementReversibleCommandCapability.Instance,
            new ProjectManagementReversibleCommandRecordRequest(traceId, commandType, projectId, aggregateType, aggregateId, forwardJson, inverseJson, traceId, summary), cancellationToken);
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

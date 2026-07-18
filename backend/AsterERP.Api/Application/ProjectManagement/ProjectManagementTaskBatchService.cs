using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Diagnostics;
using System.Text.Json;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskBatchService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementTaskProgressProjector? progressProjector = null,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementSyncJournalWriter? syncJournalWriter = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null,
    ProjectManagementTaskLabelMutation? labelMutation = null,
    IProjectManagementTaskDependencyService? dependencyService = null,
    ProjectManagementTaskHierarchy? taskHierarchy = null,
    IProjectManagementTaskParticipantService? participantService = null) : IProjectManagementTaskBatchService
{
    public async Task<IReadOnlyList<ProjectManagementTaskResponse>> UpdateAsync(ProjectManagementTaskBatchUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId)) throw new ValidationException("项目不能为空");
        if (request.Items is null || request.Items.Count == 0 || request.Items.Count > 200) throw new ValidationException("批量任务数量必须在 1 到 200 之间");
        var db = databaseAccessor.GetCurrentDb();
        var project = await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == request.ProjectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        if (project.Count == 0) throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
        await Policy().EnsureCanManageTaskAsync(request.ProjectId, request.AssigneeUserId, cancellationToken);
        var ids = request.Items.Select(item => item.TaskId).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count != request.Items.Count) throw new ValidationException("批量任务不能重复");
        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == request.ProjectId && ids.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
        if (tasks.Count != ids.Count) throw new ValidationException("存在不属于当前项目或已删除的任务");
        var byId = tasks.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var beforeSnapshots = tasks.ToDictionary(item => item.Id, TaskActivitySnapshot.From, StringComparer.Ordinal);
        var nextStatus = string.IsNullOrWhiteSpace(request.Status) ? null : ProjectManagementDomainRules.RequireTaskStatus(request.Status);
        var nextPriority = string.IsNullOrWhiteSpace(request.Priority) ? null : RequirePriority(request.Priority);
        if (request.UpdateMilestone) await EnsureMilestoneAsync(request.ProjectId, request.MilestoneId, cancellationToken);
        if (request.UpdateSchedule) ProjectManagementDomainRules.ValidateDates(request.StartDate, request.DueDate, "任务");
        if (!request.UpdateParent && !string.IsNullOrWhiteSpace(request.BeforeTaskId)) throw new ValidationException("指定同级排序位置时必须同时移动父任务");
        var moveRoots = request.UpdateParent
            ? GetMoveRoots(tasks, byId).ToList()
            : [];
        var placements = new Dictionary<string, ProjectManagementTaskPlacement>(StringComparer.Ordinal);
        if (request.UpdateParent)
            foreach (var task in moveRoots)
                placements[task.Id] = await (taskHierarchy ?? new ProjectManagementTaskHierarchy())
                    .ResolvePlacementAsync(db, request.ProjectId, request.ParentTaskId, task.Id, cancellationToken);
        var labelIds = request.UpdateLabels ? NormalizeLabelIds(request.LabelIds) : null;
        if (!request.ReplaceParticipants && request.ParticipantUserIds is not null)
            throw new ValidationException("设置批量参与人时必须指定 ReplaceParticipants");
        var participantReplaceRequest = request.ReplaceParticipants
            ? new ProjectManagementTaskParticipantBatchReplaceRequest(request.ProjectId,
                request.Items.Select(item => new ProjectManagementTaskParticipantBatchReplaceItem(item.TaskId, request.ParticipantUserIds ?? [])).ToList(),
                Activity.Current?.Id ?? Guid.NewGuid().ToString("N"))
            : null;
        var labelNames = await LoadLabelNamesAsync(db, request.ProjectId, request.UpdateLabels ? tasks.Select(item => item.Id).ToList() : [], labelIds, cancellationToken);
        if (request.UpdateLabels)
        {
            var existingLabels = await db.Queryable<ProjectManagementTaskLabelEntity>()
                .Where(item => item.ProjectId == request.ProjectId && ids.Contains(item.TaskId) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var task in tasks)
                beforeSnapshots[task.Id] = beforeSnapshots[task.Id] with
                {
                    Labels = FormatLabels(existingLabels.Where(item => item.TaskId == task.Id).Select(item => item.LabelId), labelNames)
                };
        }
        var canOverrideWip = request.OverrideWip && currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementTaskOverrideWip);
        if (request.OverrideWip && !canOverrideWip)
            throw new ValidationException("没有 WIP 强制绕过权限", ErrorCodes.PermissionDenied);
        if (nextStatus == ProjectManagementDomainRules.TaskInProgress && !canOverrideWip)
        {
            var activeCount = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == request.ProjectId && !item.IsDeleted && item.Status == ProjectManagementDomainRules.TaskInProgress && !ids.Contains(item.Id)).CountAsync(cancellationToken);
            var adding = tasks.Count(item => item.Status != ProjectManagementDomainRules.TaskInProgress);
            var wipLimit = project[0].WipLimit;
            if (wipLimit is int limit && activeCount + adding > limit) throw new ValidationException("批量更新将超过项目 WIP 上限");
        }
        var now = DateTime.UtcNow;
        var actorUserId = User();
        foreach (var item in request.Items)
        {
            var task = byId[item.TaskId];
            if (task.VersionNo != item.VersionNo || item.VersionNo <= 0) throw new ValidationException($"任务 {task.TaskCode} 已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
            if (nextStatus is not null) ProjectManagementDomainRules.EnsureTaskStatusTransition(task.Status, nextStatus);
            task.Status = nextStatus ?? task.Status;
            task.Priority = nextPriority ?? task.Priority;
            if (request.AssigneeUserId is not null) task.AssigneeUserId = string.IsNullOrWhiteSpace(request.AssigneeUserId) ? null : request.AssigneeUserId.Trim();
            if (request.UpdateMilestone) task.MilestoneId = NormalizeOptional(request.MilestoneId);
            if (request.UpdateSchedule)
            {
                task.StartDate = request.StartDate;
                task.DueDate = request.DueDate;
            }
            task.BlockedReason = task.Status == ProjectManagementDomainRules.TaskBlocked ? task.BlockedReason ?? "批量阻塞" : null;
            task.VersionNo++; task.UpdatedBy = actorUserId; task.UpdatedTime = now;
        }
        List<ProjectManagementTaskEntity> finalTasks = [];
        ProjectManagementTaskParticipantBatchMutationResult? participantMutation = null;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            if (request.UpdateParent)
                await ApplyMovesAsync(db, request, moveRoots, placements, now, actorUserId, cancellationToken);
            await db.Updateable(tasks).ExecuteCommandAsync(cancellationToken);
            if (labelIds is not null)
            {
                foreach (var task in tasks)
                {
                    await (labelMutation ?? new ProjectManagementTaskLabelMutation()).ReplaceAsync(
                        db, task, labelIds, Tenant(), App(), actorUserId, now, cancellationToken);
                }
            }
            if (participantReplaceRequest is not null)
            {
                if (participantService is null) throw new InvalidOperationException("任务参与人批量服务未配置");
                participantMutation = await participantService.ReplaceParticipantsForTasksAsync(db, participantReplaceRequest, cancellationToken);
            }
            if (dependencyService is not null)
                await dependencyService.RefreshBlockedStatesAsync(request.ProjectId, cancellationToken);
            finalTasks = await db.Queryable<ProjectManagementTaskEntity>()
                .Where(item => ids.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            await WriteBatchActivityAsync(request, finalTasks, beforeSnapshots, labelIds, labelNames, now, cancellationToken);
            foreach (var task in finalTasks)
            {
                await WriteSyncAsync(task, cancellationToken);
            }

            await (progressProjector ?? new ProjectManagementTaskProgressProjector(databaseAccessor))
                .RefreshAsync(request.ProjectId, cancellationToken);
        });
        if (participantMutation is not null)
            await participantService!.PublishCommittedBatchMutationAsync(participantMutation, cancellationToken);
        foreach (var task in finalTasks) await PublishAsync(task, cancellationToken);
        return finalTasks.Select(Map).ToList();
    }

    private ProjectManagementAccessPolicy Policy() => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private async Task EnsureMilestoneAsync(string projectId, string? milestoneId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(milestoneId)) return;
        if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementMilestoneEntity>()
            .AnyAsync(item => item.Id == milestoneId.Trim() && item.ProjectId == projectId && !item.IsDeleted, cancellationToken))
            throw new ValidationException("里程碑不存在或不属于当前项目");
    }
    private async Task WriteBatchActivityAsync(
        ProjectManagementTaskBatchUpdateRequest request,
        IReadOnlyCollection<ProjectManagementTaskEntity> tasks,
        IReadOnlyDictionary<string, TaskActivitySnapshot> beforeSnapshots,
        IReadOnlyList<string>? labelIds,
        IReadOnlyDictionary<string, string> labelNames,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        var orderedTasks = request.Items.Select(item => tasks.Single(task => task.Id == item.TaskId)).ToList();
        var details = orderedTasks.Select(task => new ProjectManagementActivityBatchItem(
            "Task", task.Id, $"更新任务 {task.Title}",
            CreateChanges(beforeSnapshots[task.Id], task, labelIds is null ? null : FormatLabels(labelIds, labelNames)))).ToList();
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            Tenant(), App(), "TaskBatch", request.ProjectId, "batch.updated", $"批量更新任务（{orderedTasks.Count} 项）",
            traceId, User(), request.ProjectId, Source: "User",
            Batch: new ProjectManagementActivityBatch(traceId, orderedTasks.Count, orderedTasks.Count, 0, details),
            OccurredAt: occurredAt), cancellationToken);
    }
    private async Task WriteSyncAsync(ProjectManagementTaskEntity task, CancellationToken cancellationToken) { if (syncJournalWriter is not null) await syncJournalWriter.AppendAsync(new ProjectManagementSyncJournalEvent(Tenant(), App(), "Task", task.Id, task.ProjectId, "batch.updated", task.VersionNo, JsonSerializer.Serialize(task), User(), null, Activity.Current?.Id ?? Guid.NewGuid().ToString("N")), cancellationToken); }
    private async Task PublishAsync(ProjectManagementTaskEntity task, CancellationToken cancellationToken) { if (realtimePublisher is not null) await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(Tenant(), App(), "Task", task.Id, "task.batch-updated", task.VersionNo, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), task.ProjectId), cancellationToken); }
    private static string RequirePriority(string value) => value.Trim() is "Low" or "Medium" or "High" or "Urgent" ? value.Trim() : throw new ValidationException("任务优先级不受支持");
    private static IEnumerable<ProjectManagementTaskEntity> GetMoveRoots(
        IReadOnlyCollection<ProjectManagementTaskEntity> tasks,
        IReadOnlyDictionary<string, ProjectManagementTaskEntity> byId)
    {
        foreach (var task in tasks)
        {
            var cursor = task.ParentTaskId;
            var nestedSelection = false;
            while (!string.IsNullOrWhiteSpace(cursor) && byId.TryGetValue(cursor, out var parent))
            {
                nestedSelection = true;
                cursor = parent.ParentTaskId;
            }
            if (!nestedSelection) yield return task;
        }
    }

    private async Task ApplyMovesAsync(
        ISqlSugarClient db,
        ProjectManagementTaskBatchUpdateRequest request,
        IReadOnlyList<ProjectManagementTaskEntity> roots,
        IReadOnlyDictionary<string, ProjectManagementTaskPlacement> placements,
        DateTime now,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (roots.Count == 0) return;
        var hierarchy = taskHierarchy ?? new ProjectManagementTaskHierarchy();
        foreach (var root in roots)
        {
            var placement = placements[root.Id];
            await hierarchy.UpdateDescendantDepthsAsync(db, placement, now, actorUserId, cancellationToken);
            root.ParentTaskId = placement.Parent?.Id;
            root.Depth = placement.RootDepth;
        }
        var movedIds = roots.Select(item => item.Id).ToList();
        var siblings = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == request.ProjectId && !item.IsDeleted && !movedIds.Contains(item.Id) &&
                (request.ParentTaskId == null ? item.ParentTaskId == null : item.ParentTaskId == request.ParentTaskId))
            .OrderBy(item => item.SortOrder, OrderByType.Asc).OrderBy(item => item.CreatedTime, OrderByType.Asc).OrderBy(item => item.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var beforeIndex = string.IsNullOrWhiteSpace(request.BeforeTaskId) ? siblings.Count : siblings.FindIndex(item => item.Id == request.BeforeTaskId);
        if (beforeIndex < 0) throw new ValidationException("目标排序任务不存在或不属于目标父任务");
        var ordered = siblings.Cast<ProjectManagementTaskEntity>().ToList();
        ordered.InsertRange(beforeIndex, roots.OrderBy(item => item.SortOrder).ThenBy(item => item.CreatedTime).ThenBy(item => item.Id, StringComparer.Ordinal));
        if (ordered.Count > int.MaxValue / 1024) throw new ValidationException("同级任务数量超过排序容量");
        var existing = siblings.ToList();
        for (var index = 0; index < existing.Count; index++) existing[index].SortOrder = -index - 1;
        if (existing.Count > 0) await db.Updateable(existing).UpdateColumns(item => new { item.SortOrder }).ExecuteCommandAsync(cancellationToken);
        for (var index = 0; index < ordered.Count; index++)
        {
            var task = ordered[index];
            task.SortOrder = (index + 1) * 1024;
            if (movedIds.Contains(task.Id)) continue;
            task.VersionNo++;
            task.UpdatedBy = actorUserId;
            task.UpdatedTime = now;
        }
        if (existing.Count > 0) await db.Updateable(existing)
            .UpdateColumns(item => new { item.SortOrder, item.VersionNo, item.UpdatedBy, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }
    private static IReadOnlyList<string> NormalizeLabelIds(IReadOnlyList<string>? labelIds)
    {
        if (labelIds is null) throw new ValidationException("批量更新标签时必须提供标签列表");
        return labelIds;
    }
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private async Task<IReadOnlyDictionary<string, string>> LoadLabelNamesAsync(ISqlSugarClient db, string projectId, IReadOnlyList<string> taskIds, IReadOnlyList<string>? requestedLabelIds, CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0 && (requestedLabelIds?.Count ?? 0) == 0) return new Dictionary<string, string>(StringComparer.Ordinal);
        var existingLabelIds = taskIds.Count == 0
            ? []
            : await db.Queryable<ProjectManagementTaskLabelEntity>().Where(item => item.ProjectId == projectId && taskIds.Contains(item.TaskId) && !item.IsDeleted).Select(item => item.LabelId).ToListAsync(cancellationToken);
        var ids = existingLabelIds.Concat(requestedLabelIds ?? []).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return new Dictionary<string, string>(StringComparer.Ordinal);
        return (await db.Queryable<ProjectManagementLabelEntity>().Where(item => item.ProjectId == projectId && ids.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken))
            .ToDictionary(item => item.Id, item => item.LabelName, StringComparer.Ordinal);
    }
    private static string? FormatLabels(IEnumerable<string> ids, IReadOnlyDictionary<string, string> labelNames)
    {
        var values = ids.Select(id => labelNames.TryGetValue(id, out var name) ? name : id).OrderBy(value => value, StringComparer.Ordinal).ToList();
        return values.Count == 0 ? null : string.Join(", ", values);
    }
    private static IReadOnlyList<ProjectManagementActivityFieldChange> CreateChanges(TaskActivitySnapshot before, ProjectManagementTaskEntity after, string? labels) =>
        ProjectManagementActivityChanges.Collect(
            ProjectManagementActivityChanges.Create("Status", "状态", before.Status, after.Status),
            ProjectManagementActivityChanges.Create("Priority", "优先级", before.Priority, after.Priority),
            ProjectManagementActivityChanges.Create("AssigneeUserId", "负责人", before.AssigneeUserId, after.AssigneeUserId),
            ProjectManagementActivityChanges.Create("MilestoneId", "里程碑", before.MilestoneId, after.MilestoneId),
            ProjectManagementActivityChanges.Create("StartDate", "开始日期", before.StartDate, after.StartDate),
            ProjectManagementActivityChanges.Create("DueDate", "截止日期", before.DueDate, after.DueDate),
            ProjectManagementActivityChanges.Create("Labels", "标签", before.Labels, labels));
    private sealed record TaskActivitySnapshot(string Status, string Priority, string? AssigneeUserId, string? MilestoneId, DateTime? StartDate, DateTime? DueDate, string? Labels)
    {
        public static TaskActivitySnapshot From(ProjectManagementTaskEntity entity) => new(entity.Status, entity.Priority, entity.AssigneeUserId, entity.MilestoneId, entity.StartDate, entity.DueDate, null);
    }
    private static ProjectManagementTaskResponse Map(ProjectManagementTaskEntity entity) => new(entity.Id, entity.ProjectId, entity.MilestoneId, entity.ParentTaskId, entity.TaskCode, entity.Title, entity.Description, entity.Status, entity.Priority, entity.AssigneeUserId, entity.AssigneeEmploymentId, entity.StartDate, entity.DueDate, entity.ProgressPercent, entity.Weight, entity.EstimateMinutes, entity.ActualMinutes, entity.SortOrder, entity.Depth, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime);
}

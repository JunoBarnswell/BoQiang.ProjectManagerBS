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

public sealed class ProjectManagementTaskService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementTaskDependencyService? dependencyService = null,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null,
    IProjectManagementSyncJournalWriter? syncJournalWriter = null,
    IProjectManagementTaskProgressProjector? progressProjector = null,
    ProjectManagementTaskHierarchy? taskHierarchy = null,
    IProjectManagementReminderScheduler? reminderScheduler = null,
    IProjectManagementImConversationService? imConversationService = null) : IProjectManagementTaskService
{
    private static readonly string[] Priorities = ["Low", "Medium", "High", "Urgent"];

    public async Task<GridPageResult<ProjectManagementTaskResponse>> QueryAsync(ProjectManagementTaskQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(query.ProjectId, cancellationToken);
        var keyword = NormalizeOptional(query.Keyword);
        var status = NormalizeOptional(query.Status);
        var assignee = NormalizeOptional(query.AssigneeUserId);
        EnsureViewProtocol(query);
        var taskQuery = databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == query.ProjectId && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(keyword))
            taskQuery = taskQuery.Where(item => item.TaskCode.Contains(keyword) || item.Title.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)));
        if (!string.IsNullOrWhiteSpace(status))
            taskQuery = taskQuery.Where(item => item.Status == status);
        if (!string.IsNullOrWhiteSpace(assignee))
            taskQuery = taskQuery.Where(item => item.AssigneeUserId == assignee);
        if (!string.IsNullOrWhiteSpace(query.MilestoneId))
            taskQuery = taskQuery.Where(item => item.MilestoneId == query.MilestoneId);
        if (!string.IsNullOrWhiteSpace(query.ParentTaskId))
            taskQuery = taskQuery.Where(item => item.ParentTaskId == query.ParentTaskId);
        if (query.DueFrom.HasValue)
            taskQuery = taskQuery.Where(item => item.DueDate >= query.DueFrom);
        if (query.DueTo.HasValue)
            taskQuery = taskQuery.Where(item => item.DueDate <= query.DueTo);
        if (!query.IncludeCompleted)
            taskQuery = taskQuery.Where(item => item.Status != ProjectManagementDomainRules.TaskDone && item.Status != ProjectManagementDomainRules.TaskCancelled);

        var total = new RefAsync<int>();
        var orderedQuery = query.SortBy switch
        {
            "dueDate" => taskQuery.OrderBy(item => item.DueDate, OrderByType.Asc),
            "priority" => taskQuery.OrderBy(item => item.Priority, OrderByType.Asc),
            "status" => taskQuery.OrderBy(item => item.Status, OrderByType.Asc),
            "updated" => taskQuery.OrderBy(item => item.UpdatedTime, OrderByType.Desc),
            _ => taskQuery.OrderBy(item => item.Depth, OrderByType.Asc).OrderBy(item => item.SortOrder, OrderByType.Asc)
        };
        if (string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            orderedQuery = query.SortBy switch
            {
                "dueDate" => taskQuery.OrderBy(item => item.DueDate, OrderByType.Desc),
                "priority" => taskQuery.OrderBy(item => item.Priority, OrderByType.Desc),
                "status" => taskQuery.OrderBy(item => item.Status, OrderByType.Desc),
                "updated" => taskQuery.OrderBy(item => item.UpdatedTime, OrderByType.Asc),
                _ => taskQuery.OrderBy(item => item.Depth, OrderByType.Desc).OrderBy(item => item.SortOrder, OrderByType.Desc)
            };
        var items = await orderedQuery
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total, cancellationToken);
        var ids = items.Select(item => item.Id).ToList();
        var dependencyRows = ids.Count == 0 ? [] : await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => item.ProjectId == query.ProjectId && ids.Contains(item.SuccessorTaskId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var predecessorIds = dependencyRows.Select(item => item.PredecessorTaskId).Distinct(StringComparer.Ordinal).ToList();
        var predecessorRows = predecessorIds.Count == 0 ? [] : await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => predecessorIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var predecessorStatus = predecessorRows.ToDictionary(item => item.Id, item => item.Status, StringComparer.Ordinal);
        return new GridPageResult<ProjectManagementTaskResponse>
        {
            Total = total.Value,
            Items = items.Select(item =>
            {
                var blockers = dependencyRows.Where(dependency => dependency.SuccessorTaskId == item.Id && (!predecessorStatus.TryGetValue(dependency.PredecessorTaskId, out var predecessor) || predecessor != ProjectManagementDomainRules.TaskDone)).ToList();
                return Map(item, blockers.Count, blockers.Count == 0, blockers.Count == 0 ? item.BlockedReason : $"存在 {blockers.Count} 个未完成前置任务");
            }).ToList()
        };
    }

    public async Task<ProjectManagementTaskResponse> CreateAsync(string projectId, ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(projectId, request.AssigneeUserId, cancellationToken);
        Validate(request);
        var db = databaseAccessor.GetCurrentDb();
        var taskCode = NormalizeRequired(request.TaskCode, "任务编码不能为空");
        if (await db.Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.ProjectId == projectId && item.TaskCode == taskCode && !item.IsDeleted, cancellationToken))
            throw new ValidationException("项目内任务编码已存在");
        var parent = await ResolveParentAsync(projectId, request.ParentTaskId, cancellationToken);
        var depth = parent is null ? 0 : parent.Depth + 1;
        ProjectManagementDomainRules.EnsureTaskDepth(depth);
        var status = ProjectManagementDomainRules.RequireTaskStatus(request.Status);
        await EnsureMilestoneAsync(projectId, request.MilestoneId, cancellationToken);
        await EnsureWipAsync(projectId, status, request.OverrideWip, cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new ProjectManagementTaskEntity
        {
            TenantId = RequireTenantId(), AppCode = RequireAppCode(), ProjectId = projectId,
            MilestoneId = NormalizeOptional(request.MilestoneId), ParentTaskId = parent?.Id,
            TaskCode = taskCode, Title = NormalizeRequired(request.Title, "任务标题不能为空"), Description = NormalizeOptional(request.Description),
            Status = status, BlockedReason = status == ProjectManagementDomainRules.TaskBlocked ? "手工阻塞" : null, Priority = NormalizePriority(request.Priority), AssigneeUserId = NormalizeOptional(request.AssigneeUserId),
            AssigneeEmploymentId = NormalizeOptional(request.AssigneeEmploymentId), StartDate = request.StartDate, DueDate = request.DueDate,
            ProgressPercent = request.ProgressPercent, Weight = request.Weight, EstimateMinutes = request.EstimateMinutes,
            SortOrder = await GetNextSiblingSortOrderAsync(projectId, parent?.Id, cancellationToken), Depth = depth, VersionNo = 1, CreatedBy = RequireUserId(), CreatedTime = now
        };
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await RefreshProgressProjectionsAsync(projectId, cancellationToken);
            await WriteActivityAsync(entity, "created", $"创建任务 {entity.Title}", cancellationToken);
            await WriteSyncJournalAsync(entity, "created", cancellationToken);
        });
        await PublishInvalidationAsync(entity, "task.created", cancellationToken);
        return Map(entity);
    }

    public async Task<ProjectManagementTaskResponse> UpdateAsync(string id, ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(entity.ProjectId, request.AssigneeUserId, cancellationToken);
        Validate(request);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var status = ProjectManagementDomainRules.RequireTaskStatus(request.Status);
        ProjectManagementDomainRules.EnsureTaskStatusTransition(entity.Status, status);
        await EnsureWipAsync(entity.ProjectId, status, request.OverrideWip, cancellationToken, entity.Id);
        var parent = await ResolveParentAsync(entity.ProjectId, request.ParentTaskId, cancellationToken, entity.Id);
        var depth = parent is null ? 0 : parent.Depth + 1;
        var oldDepth = entity.Depth;
        ProjectManagementDomainRules.EnsureTaskDepth(depth);
        if (parent is not null && await IsDescendantAsync(entity.ProjectId, parent.Id, entity.Id, cancellationToken))
            throw new ValidationException("任务不能移动到自己的子孙节点下");
        await EnsureMilestoneAsync(entity.ProjectId, request.MilestoneId, cancellationToken);
        var taskCode = NormalizeRequired(request.TaskCode, "任务编码不能为空");
        if (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.ProjectId == entity.ProjectId && item.TaskCode == taskCode && item.Id != entity.Id && !item.IsDeleted, cancellationToken))
            throw new ValidationException("项目内任务编码已存在");
        entity.MilestoneId = NormalizeOptional(request.MilestoneId);
        entity.ParentTaskId = parent?.Id;
        entity.Depth = depth;
        entity.TaskCode = taskCode;
        entity.Title = NormalizeRequired(request.Title, "任务标题不能为空");
        entity.Description = NormalizeOptional(request.Description);
        entity.Status = status;
        entity.BlockedReason = status == ProjectManagementDomainRules.TaskBlocked ? entity.BlockedReason ?? "手工阻塞" : null;
        entity.Priority = NormalizePriority(request.Priority);
        entity.AssigneeUserId = NormalizeOptional(request.AssigneeUserId);
        entity.AssigneeEmploymentId = NormalizeOptional(request.AssigneeEmploymentId);
        entity.StartDate = request.StartDate;
        entity.DueDate = request.DueDate;
        entity.ProgressPercent = request.ProgressPercent;
        entity.Weight = request.Weight;
        entity.EstimateMinutes = request.EstimateMinutes;
        entity.VersionNo++;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        var db = databaseAccessor.GetCurrentDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await UpdateSubtreeDepthAsync(
                entity.ProjectId,
                entity.Id,
                oldDepth,
                depth,
                entity.UpdatedTime ?? DateTime.UtcNow,
                entity.UpdatedBy ?? RequireUserId(),
                cancellationToken);
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            if (dependencyService is not null) await dependencyService.RefreshBlockedStatesAsync(entity.ProjectId, cancellationToken);
            await RefreshProgressProjectionsAsync(entity.ProjectId, cancellationToken);
            await WriteActivityAsync(entity, "updated", $"更新任务 {entity.Title}", cancellationToken);
            await WriteSyncJournalAsync(entity, "updated", cancellationToken);
        });
        if (imConversationService is not null)
        {
            await imConversationService.SynchronizeTaskLinksAsync(entity.Id, cancellationToken);
        }
        await PublishInvalidationAsync(entity, "task.updated", cancellationToken);
        return Map(entity);
    }

    public async Task<ProjectManagementTaskResponse> MoveAsync(string id, ProjectManagementTaskMoveRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(entity.ProjectId, entity.AssigneeUserId, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var parent = await ResolveParentAsync(entity.ProjectId, request.ParentTaskId, cancellationToken, entity.Id);
        if (request.UpdateMilestone) await EnsureMilestoneAsync(entity.ProjectId, request.MilestoneId, cancellationToken);
        var depth = parent is null ? 0 : parent.Depth + 1;
        var oldDepth = entity.Depth;
        ProjectManagementDomainRules.EnsureTaskDepth(depth);
        if (parent is not null && await IsDescendantAsync(entity.ProjectId, parent.Id, entity.Id, cancellationToken))
            throw new ValidationException("任务不能移动到自己的子孙节点下");
        var db = databaseAccessor.GetCurrentDb();
        var siblings = await LoadSiblingsAsync(entity.ProjectId, parent?.Id, entity.Id, cancellationToken);
        var orderedSiblings = InsertAtRequestedPosition(entity, siblings, request);
        var requiresRebalance = !TryAssignSparseSortOrder(entity, orderedSiblings);
        var now = DateTime.UtcNow;
        var userId = RequireUserId();
        entity.ParentTaskId = parent?.Id;
        entity.Depth = depth;
        if (request.UpdateMilestone) entity.MilestoneId = NormalizeOptional(request.MilestoneId);
        entity.VersionNo++;
        entity.UpdatedBy = userId;
        entity.UpdatedTime = now;
        try
        {
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                if (requiresRebalance)
                    await RebalanceSiblingOrderingAsync(db, entity, orderedSiblings, parent?.Id, now, userId, cancellationToken);

                await UpdateSubtreeDepthAsync(entity.ProjectId, entity.Id, oldDepth, depth, now, userId, cancellationToken);
                await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
                if (request.UpdateMilestone)
                    await UpdateSubtreeMilestoneAsync(entity.ProjectId, entity.Id, entity.MilestoneId, now, userId, cancellationToken);
                await RefreshProgressProjectionsAsync(entity.ProjectId, cancellationToken);
                await WriteActivityAsync(entity, "moved", $"移动任务 {entity.Title}", cancellationToken);
                await WriteSyncJournalAsync(entity, "moved", cancellationToken);
            });
        }
        catch (Exception exception) when (IsSiblingSortConflict(exception))
        {
            throw new ValidationException("同级任务排序已被其他用户调整，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        }
        await PublishInvalidationAsync(entity, "task.moved", cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(entity.ProjectId, entity.AssigneeUserId, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        var db = databaseAccessor.GetCurrentDb();
        var now = DateTime.UtcNow;
        var userId = RequireUserId();
        var subtree = (await (taskHierarchy ?? new ProjectManagementTaskHierarchy()).LoadSubtreeAsync(db, entity.ProjectId, entity.Id, cancellationToken))
            .Where(item => !item.IsDeleted)
            .ToList();
        var subtreeIds = subtree.Select(item => item.Id).ToList();
        if (imConversationService is not null)
        {
            await imConversationService.ArchiveTaskLinksAsync(subtreeIds, cancellationToken);
        }
        var canceledReminderJobIds = new List<string>();
        foreach (var task in subtree)
        {
            task.IsDeleted = true;
            task.DeletedBy = userId;
            task.DeletedTime = now;
            task.UpdatedBy = userId;
            task.UpdatedTime = now;
            task.VersionNo++;
        }
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(subtree)
                .UpdateColumns(item => new { item.IsDeleted, item.DeletedBy, item.DeletedTime, item.UpdatedBy, item.UpdatedTime, item.VersionNo })
                .ExecuteCommandAsync(cancellationToken);
            var pendingReminders = await db.Queryable<ProjectManagementTaskReminderEntity>()
                .Where(item => subtreeIds.Contains(item.TaskId) && item.Status == "Pending" && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            if (pendingReminders.Count > 0)
            {
                foreach (var reminder in pendingReminders)
                {
                    reminder.Status = "Canceled";
                    reminder.UpdatedBy = userId;
                    reminder.UpdatedTime = now;
                    reminder.VersionNo++;
                    if (!string.IsNullOrWhiteSpace(reminder.HangfireJobId)) canceledReminderJobIds.Add(reminder.HangfireJobId);
                }
                await db.Updateable(pendingReminders)
                    .UpdateColumns(item => new { item.Status, item.UpdatedBy, item.UpdatedTime, item.VersionNo })
                    .ExecuteCommandAsync(cancellationToken);
            }
            await RefreshProgressProjectionsAsync(entity.ProjectId, cancellationToken);
            await WriteActivityAsync(entity, "deleted", subtree.Count == 1 ? $"删除任务 {entity.Title}" : $"删除任务树 {entity.Title}（共 {subtree.Count} 项）", cancellationToken);
            foreach (var task in subtree)
                await WriteSyncJournalAsync(task, "deleted", cancellationToken);
        });
        if (reminderScheduler is not null)
            foreach (var jobId in canceledReminderJobIds)
                await reminderScheduler.DeleteAsync(jobId, cancellationToken);
        await PublishInvalidationAsync(entity, subtree.Count == 1 ? "task.deleted" : "task.subtree-deleted", cancellationToken);
    }

    public async Task<ProjectManagementTaskResponse> RestoreAsync(string id, long versionNo, CancellationToken cancellationToken = default)
    {
        RequireTenantId(); RequireAppCode();
        var db = databaseAccessor.GetCurrentDb();
        var rows = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.Id == id && item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken);
        var entity = rows.FirstOrDefault() ?? throw new NotFoundException("已删除任务不存在", ErrorCodes.PlatformResourceNotFound);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(entity.ProjectId, entity.AssigneeUserId, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        if (await db.Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.ProjectId == entity.ProjectId && item.TaskCode == entity.TaskCode && !item.IsDeleted, cancellationToken))
            throw new ValidationException("任务编码已被其他任务占用，不能恢复");
        if (!string.IsNullOrWhiteSpace(entity.ParentTaskId) && !await db.Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.Id == entity.ParentTaskId && item.ProjectId == entity.ProjectId && !item.IsDeleted, cancellationToken))
            throw new ValidationException("父任务已删除或不存在，不能恢复");
        if (entity.MilestoneId is not null && !await db.Queryable<ProjectManagementMilestoneEntity>().AnyAsync(item => item.Id == entity.MilestoneId && item.ProjectId == entity.ProjectId && !item.IsDeleted, cancellationToken))
            throw new ValidationException("里程碑已删除或不存在，不能恢复");
        entity.IsDeleted = false;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        entity.VersionNo++;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await RefreshProgressProjectionsAsync(entity.ProjectId, cancellationToken);
            await WriteActivityAsync(entity, "restored", $"恢复任务 {entity.Title}", cancellationToken);
            await WriteSyncJournalAsync(entity, "restored", cancellationToken);
        });
        if (imConversationService is not null)
        {
            await imConversationService.ReactivateTaskLinksAsync([entity.Id], cancellationToken);
        }
        await PublishInvalidationAsync(entity, "task.restored", cancellationToken);
        return Map(entity);
    }

    private async Task PublishInvalidationAsync(ProjectManagementTaskEntity entity, string eventType, CancellationToken cancellationToken)
    {
        if (realtimePublisher is null) return;
        await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(RequireTenantId(), RequireAppCode(), "Task", entity.Id, eventType, entity.VersionNo, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), entity.ProjectId), cancellationToken);
    }

    private async Task WriteActivityAsync(ProjectManagementTaskEntity entity, string activityType, string summary, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(RequireTenantId(), RequireAppCode(), "Task", entity.Id, activityType, summary, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), entity.ProjectId), cancellationToken);
    }

    private async Task WriteSyncJournalAsync(ProjectManagementTaskEntity entity, string operation, CancellationToken cancellationToken)
    {
        if (syncJournalWriter is null) return;
        await syncJournalWriter.AppendAsync(new ProjectManagementSyncJournalEvent(
            RequireTenantId(), RequireAppCode(), "Task", entity.Id, entity.ProjectId, operation, entity.VersionNo,
            JsonSerializer.Serialize(entity), RequireUserId(), null, Activity.Current?.Id ?? Guid.NewGuid().ToString("N")), cancellationToken);
    }

    private async Task EnsureProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        RequireTenantId(); RequireAppCode();
        if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && !item.IsDeleted).AnyAsync(cancellationToken))
            throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementTaskEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        RequireTenantId(); RequireAppCode();
        var entity = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == id && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        return entity.FirstOrDefault() ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementTaskEntity?> ResolveParentAsync(string projectId, string? parentId, CancellationToken cancellationToken, string? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(parentId)) return null;
        if (string.Equals(parentId, currentId, StringComparison.Ordinal)) throw new ValidationException("任务不能成为自己的父任务");
        var parent = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == parentId && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        return parent.FirstOrDefault() ?? throw new ValidationException("父任务不存在或不属于当前项目");
    }

    private async Task<bool> IsDescendantAsync(string projectId, string candidateParentId, string taskId, CancellationToken cancellationToken)
    {
        var links = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var parents = links.ToDictionary(item => item.Id, item => item.ParentTaskId, StringComparer.Ordinal);
        var cursor = candidateParentId;
        for (var index = 0; index <= ProjectManagementDomainRules.MaxTaskDepth && !string.IsNullOrWhiteSpace(cursor); index++)
        {
            if (string.Equals(cursor, taskId, StringComparison.Ordinal)) return true;
            parents.TryGetValue(cursor, out cursor);
        }
        return false;
    }

    private async Task EnsureMilestoneAsync(string projectId, string? milestoneId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(milestoneId)) return;
        if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementMilestoneEntity>()
            .Where(item => item.Id == milestoneId && item.ProjectId == projectId && !item.IsDeleted)
            .AnyAsync(cancellationToken))
            throw new ValidationException("里程碑不存在或不属于当前项目");
    }

    private async Task RefreshProgressProjectionsAsync(string projectId, CancellationToken cancellationToken)
    {
        await (progressProjector ?? new ProjectManagementTaskProgressProjector(databaseAccessor)).RefreshAsync(projectId, cancellationToken);
    }

    private async Task UpdateSubtreeDepthAsync(string projectId, string rootId, int oldDepth, int newDepth, DateTime now, string userId, CancellationToken cancellationToken)
    {
        var delta = newDepth - oldDepth;
        if (delta == 0) return;
        var tasks = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var children = tasks.GroupBy(item => item.ParentTaskId ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList(), StringComparer.Ordinal);
        var queue = new Queue<string>(new[] { rootId });
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!visited.Add(currentId)) continue;
            var current = tasks.FirstOrDefault(item => item.Id == currentId);
            if (current is not null && current.Id != rootId)
            {
                current.Depth += delta;
                current.VersionNo++;
                current.UpdatedBy = userId;
                current.UpdatedTime = now;
            }
            if (children.TryGetValue(current?.Id ?? string.Empty, out var childIds))
                foreach (var childId in childIds) queue.Enqueue(childId);
        }
        var descendants = tasks.Where(item => visited.Contains(item.Id) && item.Id != rootId).ToList();
        if (descendants.Count > 0)
            await databaseAccessor.GetCurrentDb().Updateable(descendants)
                .UpdateColumns(item => new { item.Depth, item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
    }

    private async Task UpdateSubtreeMilestoneAsync(string projectId, string rootId, string? milestoneId, DateTime now, string userId, CancellationToken cancellationToken)
    {
        var tasks = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var children = tasks.GroupBy(item => item.ParentTaskId ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList(), StringComparer.Ordinal);
        var queue = new Queue<string>(new[] { rootId });
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!visited.Add(currentId)) continue;
            if (children.TryGetValue(currentId, out var childIds))
                foreach (var childId in childIds) queue.Enqueue(childId);
        }

        var descendants = tasks.Where(item => visited.Contains(item.Id) && item.Id != rootId).ToList();
        foreach (var descendant in descendants)
        {
            descendant.MilestoneId = milestoneId;
            descendant.VersionNo++;
            descendant.UpdatedBy = userId;
            descendant.UpdatedTime = now;
        }
        if (descendants.Count > 0)
            await databaseAccessor.GetCurrentDb().Updateable(descendants)
                .UpdateColumns(item => new { item.MilestoneId, item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<List<ProjectManagementTaskEntity>> LoadSiblingsAsync(string projectId, string? parentTaskId, string excludedTaskId, CancellationToken cancellationToken)
    {
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted && item.Id != excludedTaskId &&
                (parentTaskId == null ? item.ParentTaskId == null : item.ParentTaskId == parentTaskId))
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .OrderBy(item => item.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return rows;
    }

    private static List<ProjectManagementTaskEntity> InsertAtRequestedPosition(ProjectManagementTaskEntity entity, List<ProjectManagementTaskEntity> siblings, ProjectManagementTaskMoveRequest request)
    {
        var ordered = siblings.ToList();
        var beforeTaskId = NormalizeOptional(request.BeforeTaskId);
        var index = beforeTaskId is null
            ? Math.Clamp(request.SortOrder, 0, ordered.Count)
            : ordered.FindIndex(item => item.Id == beforeTaskId);
        if (beforeTaskId is not null && index < 0)
            throw new ValidationException("目标排序任务不存在或不属于目标父任务");
        ordered.Insert(index, entity);
        return ordered;
    }

    private static bool TryAssignSparseSortOrder(ProjectManagementTaskEntity entity, IReadOnlyList<ProjectManagementTaskEntity> ordered)
    {
        var index = -1;
        for (var candidateIndex = 0; candidateIndex < ordered.Count; candidateIndex++)
        {
            if (!string.Equals(ordered[candidateIndex].Id, entity.Id, StringComparison.Ordinal)) continue;
            index = candidateIndex;
            break;
        }
        if (index < 0) throw new InvalidOperationException("任务不在目标同级排序集合中");
        var previous = index > 0 ? ordered[index - 1].SortOrder : (int?)null;
        var next = index < ordered.Count - 1 ? ordered[index + 1].SortOrder : (int?)null;
        if (previous is null && next is null)
        {
            entity.SortOrder = 1024;
            return true;
        }
        if (previous is null && next > 1)
        {
            entity.SortOrder = next.Value / 2;
            return true;
        }
        if (next is null && previous <= int.MaxValue - 1024)
        {
            entity.SortOrder = previous.Value + 1024;
            return true;
        }
        if (previous is not null && next is not null && next.Value - previous.Value > 1)
        {
            entity.SortOrder = previous.Value + (next.Value - previous.Value) / 2;
            return true;
        }
        return false;
    }

    private static async Task RebalanceSiblingOrderingAsync(ISqlSugarClient db, ProjectManagementTaskEntity entity, IReadOnlyList<ProjectManagementTaskEntity> ordered, string? targetParentTaskId, DateTime now, string userId, CancellationToken cancellationToken)
    {
        if (ordered.Count > int.MaxValue / 1024)
            throw new ValidationException("同级任务数量超过排序容量");

        var sameParentMove = string.Equals(entity.ParentTaskId, targetParentTaskId, StringComparison.Ordinal);
        var temporaryRows = sameParentMove ? ordered : ordered.Where(item => item.Id != entity.Id).ToList();
        for (var index = 0; index < temporaryRows.Count; index++) temporaryRows[index].SortOrder = -index - 1;
        if (temporaryRows.Count > 0)
            await db.Updateable(temporaryRows.ToList()).UpdateColumns(item => new { item.SortOrder }).ExecuteCommandAsync(cancellationToken);

        var changedSiblings = new List<ProjectManagementTaskEntity>();
        for (var index = 0; index < ordered.Count; index++)
        {
            var task = ordered[index];
            task.SortOrder = (index + 1) * 1024;
            if (task.Id == entity.Id) continue;
            task.VersionNo++;
            task.UpdatedBy = userId;
            task.UpdatedTime = now;
            changedSiblings.Add(task);
        }
        entity.SortOrder = ordered.Single(item => item.Id == entity.Id).SortOrder;
        if (changedSiblings.Count > 0)
            await db.Updateable(changedSiblings)
                .UpdateColumns(item => new { item.SortOrder, item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<int> GetNextSiblingSortOrderAsync(string projectId, string? parentTaskId, CancellationToken cancellationToken)
    {
        var siblings = await LoadSiblingsAsync(projectId, parentTaskId, string.Empty, cancellationToken);
        if (siblings.Count == 0) return 1024;
        var last = siblings[^1].SortOrder;
        if (last > int.MaxValue - 1024) throw new ValidationException("同级任务排序空间已满，请先重平衡");
        return Math.Max(1024, last + 1024);
    }

    private static bool IsSiblingSortConflict(Exception exception) =>
        exception.ToString().Contains("ux_pm_tasks_sibling_sort_v2", StringComparison.OrdinalIgnoreCase) ||
        exception.ToString().Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);

    private async Task EnsureWipAsync(string projectId, string status, bool overrideWip, CancellationToken cancellationToken, string? currentTaskId = null)
    {
        if (!string.Equals(status, "InProgress", StringComparison.Ordinal)) return;
        var project = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        var limit = project.FirstOrDefault()?.WipLimit;
        if (!limit.HasValue || overrideWip && currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementTaskOverrideWip)) return;
        var count = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().CountAsync(item => item.ProjectId == projectId && item.Status == "InProgress" && !item.IsDeleted && item.Id != (currentTaskId ?? string.Empty), cancellationToken);
        if (count >= limit.Value) throw new ValidationException("项目 WIP 上限已达到，需要 WIP 强制绕过权限");
    }

    private static void Validate(ProjectManagementTaskUpsertRequest request)
    {
        ProjectManagementDomainRules.ValidateDates(request.StartDate, request.DueDate, "任务");
        ProjectManagementDomainRules.RequireProgress(request.ProgressPercent, "任务");
        if (request.Weight <= 0) throw new ValidationException("任务权重必须大于 0");
        if (request.EstimateMinutes is < 0) throw new ValidationException("预估工时不能为负数");
    }

    private static string NormalizePriority(string value) => Priorities.Contains(value.Trim(), StringComparer.Ordinal) ? value.Trim() : throw new ValidationException("任务优先级不受支持");
    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string NormalizeRequired(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("任务已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static void EnsureViewProtocol(ProjectManagementTaskQuery query)
    {
        if (!new[] { "tree", "list", "card", "board", "gantt", "calendar" }.Contains(query.ViewKey, StringComparer.OrdinalIgnoreCase)) throw new ValidationException("任务视图类型不受支持");
        if (!new[] { "tree", "dueDate", "priority", "status", "updated" }.Contains(query.SortBy, StringComparer.OrdinalIgnoreCase)) throw new ValidationException("任务排序字段不受支持");
        if (!string.IsNullOrWhiteSpace(query.GroupBy) && !new[] { "status", "priority", "assignee", "milestone", "parent" }.Contains(query.GroupBy, StringComparer.OrdinalIgnoreCase)) throw new ValidationException("任务分组字段不受支持");
    }

    private static ProjectManagementTaskResponse Map(ProjectManagementTaskEntity entity, int blockedByCount = 0, bool canStart = true, string? blockedReason = null) => new(entity.Id, entity.ProjectId, entity.MilestoneId, entity.ParentTaskId, entity.TaskCode, entity.Title, entity.Description, entity.Status, entity.Priority, entity.AssigneeUserId, entity.AssigneeEmploymentId, entity.StartDate, entity.DueDate, entity.ProgressPercent, entity.Weight, entity.EstimateMinutes, entity.ActualMinutes, entity.SortOrder, entity.Depth, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime, blockedByCount, canStart, blockedReason);
}

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
    IProjectManagementImConversationService? imConversationService = null,
    ProjectManagementTaskLabelMutation? labelMutation = null,
    IProjectManagementTaskTemplateDependencyCommandService? templateDependencyCommand = null,
    ProjectManagementTaskStateMachine? taskStateMachine = null) : IProjectManagementTaskService, IProjectManagementTaskOccurrenceCommandService, IProjectManagementTaskTemplateCommandService
{
    private static readonly string[] Priorities = ["Low", "Medium", "High", "Urgent"];

    public async Task<GridPageResult<ProjectManagementTaskListItemResponse>> QueryAsync(ProjectManagementTaskQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(query.ProjectId, cancellationToken);
        await AccessPolicy.EnsureCanViewProjectAsync(query.ProjectId, cancellationToken);
        var keyword = NormalizeOptional(query.Keyword);
        var status = NormalizeOptional(query.Status);
        var assignee = NormalizeOptional(query.AssigneeUserId);
        var labelFilter = ProjectManagementTaskLabelFilterQuery.Normalize(query.LabelFilter);
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        EnsureViewProtocol(query);
        ValidateQuery(query, status);
        var taskQuery = databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == query.ProjectId && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(keyword))
            taskQuery = taskQuery.Where(item => item.TaskCode.Contains(keyword) || item.Title.Contains(keyword) ||
                (item.Summary != null && item.Summary.Contains(keyword)) || (item.Description != null && item.Description.Contains(keyword)));
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
        taskQuery = ProjectManagementTaskLabelFilterQuery.ApplyToTasks(taskQuery, labelFilter, tenantId, appCode);

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
        return new GridPageResult<ProjectManagementTaskListItemResponse>
        {
            Total = total.Value,
            Items = items.Select(item =>
            {
                var blockers = dependencyRows.Where(dependency => dependency.SuccessorTaskId == item.Id && (!predecessorStatus.TryGetValue(dependency.PredecessorTaskId, out var predecessor) || predecessor != ProjectManagementDomainRules.TaskDone)).ToList();
                var forceStarted = IsForceStarted(item.BlockedReason);
                return MapList(item, blockers.Count, blockers.Count == 0 || forceStarted, blockers.Count == 0 || forceStarted ? item.BlockedReason : $"存在 {blockers.Count} 个未完成前置任务");
            }).ToList()
        };
    }

    public async Task<ProjectManagementTaskDetailResponse> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        await AccessPolicy.EnsureCanViewProjectAsync(entity.ProjectId, cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    public Task<ProjectManagementTaskDetailResponse> CreateAsync(string projectId, ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(projectId, request, null, cancellationToken);

    private async Task<ProjectManagementTaskDetailResponse> CreateCoreAsync(
        string projectId,
        ProjectManagementTaskUpsertRequest request,
        ProjectManagementTaskRecurrenceOccurrenceEntity? recurrenceOccurrence,
        CancellationToken cancellationToken)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        Validate(request);
        var db = databaseAccessor.GetCurrentDb();
        var taskCode = NormalizeRequired(request.TaskCode, "任务编码不能为空");
        if (await db.Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.ProjectId == projectId && item.TaskCode == taskCode && !item.IsDeleted, cancellationToken))
            throw new ValidationException("项目内任务编码已存在");
        var placement = await TaskHierarchy.ResolvePlacementAsync(db, projectId, request.ParentTaskId, null, cancellationToken);
        var parent = placement.Parent;
        await EnsureTaskWriteAccessAsync(projectId, null, parent?.Id, request.AssigneeUserId, cancellationToken);
        await EnsureAssigneeAsync(projectId, request.AssigneeUserId, cancellationToken);
        var depth = placement.RootDepth;
        var state = StateMachine.Resolve(string.Empty, request.Status, request.ProgressPercent, null, null, now: DateTime.UtcNow, isNew: true);
        await EnsureMilestoneAsync(projectId, request.MilestoneId, cancellationToken);
        await EnsureWipAsync(projectId, state.Status, request.OverrideWip, cancellationToken);
        var now = DateTime.UtcNow;
        var weight = ResolveProgressWeight(request.Weight, request.EstimateMinutes);
        var entity = new ProjectManagementTaskEntity
        {
            TenantId = RequireTenantId(), AppCode = RequireAppCode(), ProjectId = projectId,
            MilestoneId = NormalizeOptional(request.MilestoneId), ParentTaskId = parent?.Id,
            TaskCode = taskCode, Title = NormalizeRequired(request.Title, "任务标题不能为空"), Summary = NormalizeOptional(request.Summary), Description = ResolveMarkdown(request),
            Status = state.Status, BlockedReason = state.Status == ProjectManagementDomainRules.TaskBlocked ? "手工阻塞" : null, Priority = NormalizePriority(request.Priority), AssigneeUserId = NormalizeOptional(request.AssigneeUserId),
            AssigneeEmploymentId = NormalizeOptional(request.AssigneeEmploymentId), StartDate = request.StartDate, DueDate = request.DueDate,
            ActualStartAt = state.ActualStartAt, ActualEndAt = state.ActualEndAt, ProgressPercent = state.ProgressPercent, Weight = weight, EstimateMinutes = request.EstimateMinutes,
            SortOrder = await GetNextSiblingSortOrderAsync(projectId, parent?.Id, cancellationToken), Depth = depth, VersionNo = 1, CreatedBy = RequireUserId(), CreatedTime = now
        };
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            if (recurrenceOccurrence is not null)
            {
                recurrenceOccurrence.State = "Generating";
                await db.Insertable(recurrenceOccurrence).ExecuteCommandAsync(cancellationToken);
            }
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            if (recurrenceOccurrence is not null)
            {
                recurrenceOccurrence.TaskId = entity.Id;
                recurrenceOccurrence.State = "Generated";
                recurrenceOccurrence.UpdatedBy = RequireUserId();
                recurrenceOccurrence.UpdatedTime = DateTime.UtcNow;
                await db.Updateable(recurrenceOccurrence)
                    .UpdateColumns(item => new { item.TaskId, item.State, item.UpdatedBy, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }
            await RefreshProgressProjectionsAsync(projectId, cancellationToken);
            await WriteActivityAsync(entity, "created", $"创建任务 {entity.Title}", cancellationToken);
            await WriteSyncJournalAsync(entity, "created", cancellationToken);
        });
        await PublishInvalidationAsync(entity, "task.created", cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    public async Task<ProjectManagementTaskRecurrenceOccurrenceEntity> CreateOccurrenceAsync(
        ProjectManagementTaskOccurrenceCapability capability,
        ProjectManagementTaskRecurrenceEntity recurrence,
        ProjectManagementTaskRecurrenceOccurrenceEntity occurrence,
        ProjectManagementTaskUpsertRequest task,
        CancellationToken cancellationToken = default)
    {
        EnsureOccurrenceCapability(capability, recurrence);
        var existing = await FindOccurrenceAsync(recurrence.Id, occurrence.RecurrenceKey, cancellationToken);
        if (existing is not null) return existing;
        try
        {
            var created = await CreateCoreAsync(recurrence.ProjectId, task, occurrence, cancellationToken);
            occurrence.TaskId = created.Id;
            occurrence.State = "Generated";
            return occurrence;
        }
        catch (Exception exception) when (IsRecurrenceKeyConflict(exception))
        {
            var concurrent = await FindOccurrenceAsync(recurrence.Id, occurrence.RecurrenceKey, cancellationToken);
            if (concurrent is not null) return concurrent;
            throw;
        }
    }

    public async Task<ProjectManagementTaskTemplateCreationResult> CreateTemplateAsync(ProjectManagementTaskTemplateCapability capability, string projectId, IReadOnlyList<ProjectManagementTaskTemplateNodeCreateCommand> nodes, IReadOnlyList<ProjectManagementTaskTemplateDependencyCreateCommand> dependencies, CancellationToken cancellationToken = default)
    {
        if (!ReferenceEquals(capability, ProjectManagementTaskTemplateCapability.Instance)) throw new InvalidOperationException("任务模板命令缺少内部 capability");
        if (nodes.Count is < 1 or > 1000 || nodes.Select(item => item.NodeKey).Distinct(StringComparer.Ordinal).Count() != nodes.Count) throw new ValidationException("模板任务节点无效");
        await EnsureProjectAsync(projectId, cancellationToken);
        await EnsureTaskWriteAccessAsync(projectId, null, null, null, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var created = new Dictionary<string, ProjectManagementTaskEntity>(StringComparer.Ordinal);
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            foreach (var node in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parentId = string.IsNullOrWhiteSpace(node.Task.ParentTaskId) ? null : created.GetValueOrDefault(node.Task.ParentTaskId!)?.Id
                    ?? throw new ValidationException("模板父任务节点不存在或排序无效");
                var task = node.Task with { ParentTaskId = parentId };
                var entity = await PrepareTemplateTaskAsync(projectId, task, cancellationToken);
                await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
                if (node.LabelIds.Count > 0) await (labelMutation ?? new ProjectManagementTaskLabelMutation()).ReplaceAsync(db, entity, node.LabelIds, RequireTenantId(), RequireAppCode(), RequireUserId(), DateTime.UtcNow, cancellationToken);
                await WriteActivityAsync(entity, "created", $"从模板创建任务 {entity.Title}", cancellationToken);
                await WriteSyncJournalAsync(entity, "created", cancellationToken);
                created.Add(node.NodeKey, entity);
            }
            if (dependencies.Count > 0)
            {
                var mapped = dependencies.Select(item => new ProjectManagementTaskDependencyUpsertRequest(
                    created.GetValueOrDefault(item.PredecessorNodeKey)?.Id ?? throw new ValidationException("模板前置任务不存在"),
                    created.GetValueOrDefault(item.SuccessorNodeKey)?.Id ?? throw new ValidationException("模板后置任务不存在"), item.DependencyType, item.LagMinutes)).ToList();
                var command = templateDependencyCommand ?? throw new InvalidOperationException("任务模板依赖命令尚未注册");
                await command.CreateBatchInTransactionAsync(ProjectManagementTaskTemplateDependencyCapability.Instance, projectId, new ProjectManagementTaskDependencyBatchCreateRequest(mapped), cancellationToken);
            }
            await RefreshProgressProjectionsAsync(projectId, cancellationToken);
        });
        foreach (var task in created.Values) await PublishInvalidationAsync(task, "task.created-from-template", cancellationToken);
        return new ProjectManagementTaskTemplateCreationResult(created.ToDictionary(pair => pair.Key, pair => ToTemplateResponse(pair.Value), StringComparer.Ordinal));
    }

    public async Task UpdateFutureAsync(
        ProjectManagementTaskOccurrenceCapability capability,
        ProjectManagementTaskRecurrenceEntity recurrence,
        IReadOnlyList<ProjectManagementTaskRecurrenceOccurrenceEntity> occurrences,
        ProjectManagementTaskUpsertRequest task,
        CancellationToken cancellationToken = default)
    {
        EnsureOccurrenceCapability(capability, recurrence);
        foreach (var occurrence in occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = await GetRequiredAsync(occurrence.TaskId, cancellationToken);
            if (existing.Status is ProjectManagementDomainRules.TaskDone or ProjectManagementDomainRules.TaskCancelled) continue;
            var update = task with { TaskCode = existing.TaskCode, VersionNo = existing.VersionNo };
            await UpdateAsync(existing.Id, update, cancellationToken);
        }
    }

    public async Task DeleteFutureAsync(
        ProjectManagementTaskOccurrenceCapability capability,
        ProjectManagementTaskRecurrenceEntity recurrence,
        IReadOnlyList<ProjectManagementTaskRecurrenceOccurrenceEntity> occurrences,
        CancellationToken cancellationToken = default)
    {
        EnsureOccurrenceCapability(capability, recurrence);
        var db = databaseAccessor.GetCurrentDb();
        foreach (var occurrence in occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = await GetRequiredAsync(occurrence.TaskId, cancellationToken);
            if (existing.Status is ProjectManagementDomainRules.TaskDone or ProjectManagementDomainRules.TaskCancelled) continue;
            await DeleteAsync(existing.Id, new ProjectManagementTaskDeleteRequest(existing.VersionNo), cancellationToken);
            occurrence.State = "Deleted";
            occurrence.VersionNo++;
            occurrence.UpdatedBy = RequireUserId();
            occurrence.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(occurrence)
                .UpdateColumns(item => new { item.State, item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<ProjectManagementTaskDetailResponse> UpdateAsync(string id, ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        Validate(request);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var state = StateMachine.Resolve(entity.Status, request.Status, request.ProgressPercent, entity.ActualStartAt, entity.ActualEndAt, DateTime.UtcNow);
        var db = databaseAccessor.GetCurrentDb();
        var placement = await TaskHierarchy.ResolvePlacementAsync(db, entity.ProjectId, request.ParentTaskId, entity.Id, cancellationToken);
        var parent = placement.Parent;
        await EnsureTaskWriteAccessAsync(entity.ProjectId, entity.Id, parent?.Id, request.AssigneeUserId, cancellationToken);
        await EnsureAssigneeAsync(entity.ProjectId, request.AssigneeUserId, cancellationToken);
        await EnsureWipAsync(entity.ProjectId, state.Status, request.OverrideWip, cancellationToken, entity.Id);
        if (state.Status == ProjectManagementDomainRules.TaskDone && entity.Status != ProjectManagementDomainRules.TaskDone)
            await EnsureCanCompleteParentAsync(entity, request, cancellationToken);
        var depth = placement.RootDepth;
        await EnsureMilestoneAsync(entity.ProjectId, request.MilestoneId, cancellationToken);
        var taskCode = NormalizeRequired(request.TaskCode, "任务编码不能为空");
        if (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.ProjectId == entity.ProjectId && item.TaskCode == taskCode && item.Id != entity.Id && !item.IsDeleted, cancellationToken))
            throw new ValidationException("项目内任务编码已存在");
        entity.MilestoneId = NormalizeOptional(request.MilestoneId);
        entity.ParentTaskId = parent?.Id;
        entity.Depth = depth;
        entity.TaskCode = taskCode;
        entity.Title = NormalizeRequired(request.Title, "任务标题不能为空");
        entity.Summary = NormalizeOptional(request.Summary);
        entity.Description = ResolveMarkdown(request);
        entity.Status = state.Status;
        entity.BlockedReason = state.Status == ProjectManagementDomainRules.TaskBlocked ? entity.BlockedReason ?? "手工阻塞" : null;
        entity.Priority = NormalizePriority(request.Priority);
        entity.AssigneeUserId = NormalizeOptional(request.AssigneeUserId);
        entity.AssigneeEmploymentId = NormalizeOptional(request.AssigneeEmploymentId);
        entity.StartDate = request.StartDate;
        entity.DueDate = request.DueDate;
        entity.ActualStartAt = state.ActualStartAt;
        entity.ActualEndAt = state.ActualEndAt;
        entity.ProgressPercent = state.ProgressPercent;
        entity.Weight = ResolveProgressWeight(request.Weight, request.EstimateMinutes);
        entity.EstimateMinutes = request.EstimateMinutes;
        var expectedVersion = entity.VersionNo;
        entity.VersionNo++;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await TaskHierarchy.UpdateDescendantDepthsAsync(
                db, placement, entity.UpdatedTime ?? DateTime.UtcNow, entity.UpdatedBy ?? RequireUserId(), cancellationToken);
            await UpdateTaskWithExpectedVersionAsync(db, entity, expectedVersion, cancellationToken);
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
        return await MapDetailAsync(entity, cancellationToken);
    }

    public async Task<ProjectManagementTaskDependencyForceStartResponse> ForceStartAsync(string id, ProjectManagementTaskDependencyForceStartRequest request, CancellationToken cancellationToken = default)
    {
        var task = await GetRequiredAsync(id, cancellationToken);
        // 任务入口只负责定位对象与项目级 Owner/Manager 授权；阻塞判断、版本校验和审计保持在依赖聚合内。
        await AccessPolicy.EnsureCanManageProjectAsync(task.ProjectId, cancellationToken);
        if (dependencyService is null) throw new ValidationException("任务依赖服务未配置");
        return await dependencyService.ForceStartAsync(task.ProjectId, task.Id, request, cancellationToken);
    }

    public async Task<ProjectManagementTaskDetailResponse> MoveAsync(string id, ProjectManagementTaskMoveRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var db = databaseAccessor.GetCurrentDb();
        var placement = await TaskHierarchy.ResolvePlacementAsync(db, entity.ProjectId, request.ParentTaskId, entity.Id, cancellationToken);
        var parent = placement.Parent;
        await EnsureTaskWriteAccessAsync(entity.ProjectId, entity.Id, parent?.Id, entity.AssigneeUserId, cancellationToken);
        if (request.UpdateMilestone) await EnsureMilestoneAsync(entity.ProjectId, request.MilestoneId, cancellationToken);
        var depth = placement.RootDepth;
        var siblings = await LoadSiblingsAsync(entity.ProjectId, parent?.Id, entity.Id, cancellationToken);
        var orderedSiblings = InsertAtRequestedPosition(entity, siblings, request);
        var requiresRebalance = !TryAssignSparseSortOrder(entity, orderedSiblings);
        var now = DateTime.UtcNow;
        var userId = RequireUserId();
        var expectedVersion = entity.VersionNo;
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

                await TaskHierarchy.UpdateDescendantDepthsAsync(db, placement, now, userId, cancellationToken);
                await UpdateTaskWithExpectedVersionAsync(db, entity, expectedVersion, cancellationToken);
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
        return await MapDetailAsync(entity, cancellationToken);
    }

    public Task DeleteAsync(string id, long versionNo, CancellationToken cancellationToken = default) =>
        DeleteAsync(id, new ProjectManagementTaskDeleteRequest(versionNo), cancellationToken);

    public async Task DeleteAsync(string id, ProjectManagementTaskDeleteRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        await EnsureTaskWriteAccessAsync(entity.ProjectId, entity.Id, entity.ParentTaskId, entity.AssigneeUserId, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var deleteMode = ProjectManagementTaskHierarchy.RequireDeleteMode(request.Mode);
        var db = databaseAccessor.GetCurrentDb();
        var now = DateTime.UtcNow;
        var userId = RequireUserId();
        var subtree = (await TaskHierarchy.LoadSubtreeAsync(db, entity.ProjectId, entity.Id, cancellationToken))
            .Where(item => !item.IsDeleted)
            .ToList();
        var deleteTargets = deleteMode == ProjectManagementTaskHierarchy.CascadeDeleteMode ? subtree : [entity];
        var deletedTaskIds = deleteTargets.Select(item => item.Id).ToList();
        if (imConversationService is not null)
        {
            await imConversationService.ArchiveTaskLinksAsync(deletedTaskIds, cancellationToken);
        }
        var canceledReminderJobIds = new List<string>();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            foreach (var task in deleteTargets)
            {
                var expectedVersion = task.VersionNo;
                task.IsDeleted = true;
                task.DeletedBy = userId;
                task.DeletedTime = now;
                task.UpdatedBy = userId;
                task.UpdatedTime = now;
                task.VersionNo++;
                await UpdateTaskWithExpectedVersionAsync(db, task, expectedVersion, cancellationToken);
            }
            if (deleteMode == ProjectManagementTaskHierarchy.PromoteChildrenDeleteMode)
                await TaskHierarchy.PromoteChildrenAsync(db, entity, subtree, now, userId, cancellationToken);
            var pendingReminders = await db.Queryable<ProjectManagementTaskReminderEntity>()
                .Where(item => deletedTaskIds.Contains(item.TaskId) && item.Status == "Pending" && !item.IsDeleted)
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
            var summary = deleteMode == ProjectManagementTaskHierarchy.PromoteChildrenDeleteMode
                ? $"删除任务并提升子任务 {entity.Title}"
                : deleteTargets.Count == 1 ? $"删除任务 {entity.Title}" : $"删除任务树 {entity.Title}（共 {deleteTargets.Count} 项）";
            await WriteActivityAsync(entity, "deleted", summary, cancellationToken);
            foreach (var task in deleteTargets)
                await WriteSyncJournalAsync(task, "deleted", cancellationToken);
        });
        if (reminderScheduler is not null)
            foreach (var jobId in canceledReminderJobIds)
                await reminderScheduler.DeleteAsync(jobId, cancellationToken);
        await PublishInvalidationAsync(entity, deleteMode == ProjectManagementTaskHierarchy.PromoteChildrenDeleteMode
            ? "task.deleted-children-promoted"
            : deleteTargets.Count == 1 ? "task.deleted" : "task.subtree-deleted", cancellationToken);
    }

    public async Task<ProjectManagementTaskDetailResponse> RestoreAsync(string id, long versionNo, CancellationToken cancellationToken = default)
    {
        RequireTenantId(); RequireAppCode();
        var db = databaseAccessor.GetCurrentDb();
        var rows = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.Id == id && item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode() && item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken);
        var entity = rows.FirstOrDefault() ?? throw new NotFoundException("已删除任务不存在", ErrorCodes.PlatformResourceNotFound);
        await EnsureTaskWriteAccessAsync(entity.ProjectId, entity.Id, entity.ParentTaskId, entity.AssigneeUserId, cancellationToken);
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
        return await MapDetailAsync(entity, cancellationToken);
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

    private ProjectManagementAccessPolicy AccessPolicy => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private ProjectManagementTaskHierarchy TaskHierarchy => taskHierarchy ?? new ProjectManagementTaskHierarchy();
    private ProjectManagementTaskStateMachine StateMachine => taskStateMachine ?? new ProjectManagementTaskStateMachine();

    // 保持任务写入授权在单一接缝：ScopeRootTaskId 权限落地后只需在此转发任务与父任务上下文。
    private Task EnsureTaskWriteAccessAsync(string projectId, string? taskId, string? parentTaskId, string? assigneeUserId, CancellationToken cancellationToken)
        => AccessPolicy.EnsureCanManageTaskAsync(projectId, assigneeUserId, cancellationToken);

    private async Task EnsureProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .AnyAsync(cancellationToken))
            throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementTaskEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var entity = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.Id == id && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken);
        return entity.FirstOrDefault() ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task EnsureAssigneeAsync(string projectId, string? assigneeUserId, CancellationToken cancellationToken)
    {
        var normalizedAssigneeUserId = NormalizeOptional(assigneeUserId);
        if (normalizedAssigneeUserId is null) return;
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var project = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken);
        if (string.Equals(project.FirstOrDefault()?.OwnerUserId, normalizedAssigneeUserId, StringComparison.OrdinalIgnoreCase)) return;
        if (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && item.UserId == normalizedAssigneeUserId && item.IsActive && !item.IsDeleted)
            .AnyAsync(cancellationToken)) return;
        throw new ValidationException("任务负责人必须是项目负责人或有效项目成员");
    }

    private static async Task UpdateTaskWithExpectedVersionAsync(
        ISqlSugarClient db,
        ProjectManagementTaskEntity entity,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        if (await db.Updateable(entity)
            .Where(item => item.Id == entity.Id && item.VersionNo == expectedVersion)
            .ExecuteCommandAsync(cancellationToken) != 1)
            throw new ValidationException("任务已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
    }

    private async Task EnsureMilestoneAsync(string projectId, string? milestoneId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(milestoneId)) return;
        if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementMilestoneEntity>()
            .Where(item => item.Id == milestoneId && item.ProjectId == projectId && !item.IsDeleted)
            .AnyAsync(cancellationToken))
            throw new ValidationException("里程碑不存在或不属于当前项目");
    }

    private async Task EnsureCanCompleteParentAsync(ProjectManagementTaskEntity task, ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken)
    {
        var hasIncompleteChildren = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .AnyAsync(item => item.ProjectId == task.ProjectId && item.ParentTaskId == task.Id && !item.IsDeleted && item.Status != ProjectManagementDomainRules.TaskDone, cancellationToken);
        if (!hasIncompleteChildren) return;
        if (!request.ForceComplete) throw new ValidationException("存在未完成子任务，不能完成父任务");
        if (string.IsNullOrWhiteSpace(request.ForceCompleteReason)) throw new ValidationException("强制完成父任务必须填写原因");
        if (!currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementTaskOverrideWip)) throw new ValidationException("没有强制完成父任务权限", ErrorCodes.PermissionDenied);
        await AccessPolicy.EnsureCanManageProjectAsync(task.ProjectId, cancellationToken);
    }

    private async Task RefreshProgressProjectionsAsync(string projectId, CancellationToken cancellationToken)
    {
        await (progressProjector ?? new ProjectManagementTaskProgressProjector(databaseAccessor)).RefreshAsync(projectId, cancellationToken);
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

    private void EnsureOccurrenceCapability(ProjectManagementTaskOccurrenceCapability capability, ProjectManagementTaskRecurrenceEntity recurrence)
    {
        if (!ReferenceEquals(capability, ProjectManagementTaskOccurrenceCapability.Instance))
            throw new InvalidOperationException("重复任务实例命令缺少内部 capability");
        if (!string.Equals(recurrence.TenantId, RequireTenantId(), StringComparison.Ordinal) ||
            !string.Equals(recurrence.AppCode, RequireAppCode(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(recurrence.SeriesOwnerUserId, RequireUserId(), StringComparison.Ordinal))
            throw new InvalidOperationException("重复任务实例命令的系列创建者上下文不匹配");
    }

    private async Task<ProjectManagementTaskRecurrenceOccurrenceEntity?> FindOccurrenceAsync(string recurrenceId, string recurrenceKey, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskRecurrenceOccurrenceEntity>()
            .Where(item => item.RecurrenceId == recurrenceId && item.RecurrenceKey == recurrenceKey &&
                item.TenantId == RequireTenantId() && item.AppCode == RequireAppCode() && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken)).FirstOrDefault();

    private static bool IsRecurrenceKeyConflict(Exception exception) =>
        exception.ToString().Contains("ux_pm_task_recurrence_occurrences_key", StringComparison.OrdinalIgnoreCase) ||
            exception.ToString().Contains("pm_task_recurrence_occurrences.RecurrenceId", StringComparison.OrdinalIgnoreCase);

    private async Task<ProjectManagementTaskEntity> PrepareTemplateTaskAsync(string projectId, ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var db = databaseAccessor.GetCurrentDb();
        var taskCode = NormalizeRequired(request.TaskCode, "任务编码不能为空");
        if (await db.Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.ProjectId == projectId && item.TaskCode == taskCode && !item.IsDeleted, cancellationToken)) throw new ValidationException("项目内任务编码已存在");
        var placement = await TaskHierarchy.ResolvePlacementAsync(db, projectId, request.ParentTaskId, null, cancellationToken);
        var parent = placement.Parent;
        await EnsureTaskWriteAccessAsync(projectId, null, parent?.Id, request.AssigneeUserId, cancellationToken);
        await EnsureAssigneeAsync(projectId, request.AssigneeUserId, cancellationToken);
        await EnsureMilestoneAsync(projectId, request.MilestoneId, cancellationToken);
        var state = StateMachine.Resolve(string.Empty, request.Status, request.ProgressPercent, null, null, DateTime.UtcNow, isNew: true);
        await EnsureWipAsync(projectId, state.Status, request.OverrideWip, cancellationToken);
        return new ProjectManagementTaskEntity
        {
            TenantId = RequireTenantId(), AppCode = RequireAppCode(), ProjectId = projectId, MilestoneId = NormalizeOptional(request.MilestoneId), ParentTaskId = parent?.Id,
            TaskCode = taskCode, Title = NormalizeRequired(request.Title, "任务标题不能为空"), Summary = NormalizeOptional(request.Summary), Description = ResolveMarkdown(request), Status = state.Status,
            BlockedReason = state.Status == ProjectManagementDomainRules.TaskBlocked ? "手工阻塞" : null, Priority = NormalizePriority(request.Priority), AssigneeUserId = NormalizeOptional(request.AssigneeUserId), AssigneeEmploymentId = NormalizeOptional(request.AssigneeEmploymentId),
            StartDate = request.StartDate, DueDate = request.DueDate, ActualStartAt = state.ActualStartAt, ActualEndAt = state.ActualEndAt, ProgressPercent = state.ProgressPercent, Weight = ResolveProgressWeight(request.Weight, request.EstimateMinutes), EstimateMinutes = request.EstimateMinutes,
            SortOrder = await GetNextSiblingSortOrderAsync(projectId, parent?.Id, cancellationToken), Depth = placement.RootDepth, VersionNo = 1, CreatedBy = RequireUserId(), CreatedTime = DateTime.UtcNow
        };
    }

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
        if (request.Weight is <= 0) throw new ValidationException("任务权重必须大于 0");
        if (request.EstimateMinutes is < 0) throw new ValidationException("预估工时不能为负数");
        _ = ResolveMarkdown(request);
    }

    private static decimal ResolveProgressWeight(decimal? explicitWeight, int? estimateMinutes) =>
        explicitWeight ?? (estimateMinutes is > 0 ? estimateMinutes.Value : 1m);

    private static void ValidateQuery(ProjectManagementTaskQuery query, string? status)
    {
        if (status is not null) ProjectManagementDomainRules.RequireTaskStatus(status);
        if (query.DueFrom.HasValue && query.DueTo.HasValue && query.DueFrom > query.DueTo)
            throw new ValidationException("任务截止日期筛选区间无效");
    }

    private static string NormalizePriority(string value) => Priorities.Contains(value.Trim(), StringComparer.Ordinal) ? value.Trim() : throw new ValidationException("任务优先级不受支持");
    private static bool IsForceStarted(string? blockedReason) => blockedReason?.StartsWith("已强制开始：", StringComparison.Ordinal) == true;
    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string NormalizeRequired(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? ResolveMarkdown(ProjectManagementTaskUpsertRequest request)
    {
        var legacyDescription = NormalizeOptional(request.Description);
        var markdown = NormalizeOptional(request.Markdown);
        if (legacyDescription is not null && markdown is not null && !string.Equals(legacyDescription, markdown, StringComparison.Ordinal))
            throw new ValidationException("Description 与 Markdown 不能同时提供不同内容");
        return markdown ?? legacyDescription;
    }
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("任务已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static void EnsureViewProtocol(ProjectManagementTaskQuery query)
    {
        if (!new[] { "tree", "list", "card", "board", "gantt", "calendar" }.Contains(query.ViewKey, StringComparer.OrdinalIgnoreCase)) throw new ValidationException("任务视图类型不受支持");
        if (!new[] { "tree", "dueDate", "priority", "status", "updated" }.Contains(query.SortBy, StringComparer.OrdinalIgnoreCase)) throw new ValidationException("任务排序字段不受支持");
        if (!string.IsNullOrWhiteSpace(query.GroupBy) && !new[] { "status", "priority", "assignee", "milestone", "parent" }.Contains(query.GroupBy, StringComparer.OrdinalIgnoreCase)) throw new ValidationException("任务分组字段不受支持");
    }

    private async Task<ProjectManagementTaskDetailResponse> MapDetailAsync(ProjectManagementTaskEntity entity, CancellationToken cancellationToken)
    {
        var dependencyRows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => item.ProjectId == entity.ProjectId && item.SuccessorTaskId == entity.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var predecessorIds = dependencyRows.Select(item => item.PredecessorTaskId).Distinct(StringComparer.Ordinal).ToList();
        List<ProjectManagementTaskEntity> predecessors = predecessorIds.Count == 0 ? [] : await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => predecessorIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var predecessorStatus = predecessors.ToDictionary(item => item.Id, item => item.Status, StringComparer.Ordinal);
        var blockedByCount = dependencyRows.Count(dependency => !predecessorStatus.TryGetValue(dependency.PredecessorTaskId, out var status) || status != ProjectManagementDomainRules.TaskDone);
        var forceStarted = IsForceStarted(entity.BlockedReason);
        return MapDetail(entity, blockedByCount, blockedByCount == 0 || forceStarted, blockedByCount == 0 || forceStarted ? entity.BlockedReason : $"存在 {blockedByCount} 个未完成前置任务");
    }

    private static ProjectManagementTaskListItemResponse MapList(ProjectManagementTaskEntity entity, int blockedByCount, bool canStart, string? blockedReason) => new(entity.Id, entity.ProjectId, entity.MilestoneId, entity.ParentTaskId, entity.TaskCode, entity.Title, entity.Status, entity.Priority, entity.AssigneeUserId, entity.StartDate, entity.DueDate, entity.ProgressPercent, entity.SortOrder, entity.Depth, entity.VersionNo, blockedByCount, canStart, blockedReason, ProjectManagementDomainRules.IsTaskOverdue(entity.Status, entity.DueDate, DateTime.UtcNow), entity.ActualStartAt, entity.ActualEndAt, entity.Summary);

    private static ProjectManagementTaskDetailResponse MapDetail(ProjectManagementTaskEntity entity, int blockedByCount, bool canStart, string? blockedReason) => new(entity.Id, entity.ProjectId, entity.MilestoneId, entity.ParentTaskId, entity.TaskCode, entity.Title, entity.Description, entity.Status, entity.Priority, entity.AssigneeUserId, entity.AssigneeEmploymentId, entity.StartDate, entity.DueDate, entity.ProgressPercent, entity.Weight, entity.EstimateMinutes, entity.ActualMinutes, entity.SortOrder, entity.Depth, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime, blockedByCount, canStart, blockedReason, ProjectManagementDomainRules.IsTaskOverdue(entity.Status, entity.DueDate, DateTime.UtcNow), entity.ActualStartAt, entity.ActualEndAt, Summary: entity.Summary, Markdown: entity.Description);
    private static ProjectManagementTaskResponse ToTemplateResponse(ProjectManagementTaskEntity entity) => new(entity.Id, entity.ProjectId, entity.MilestoneId, entity.ParentTaskId, entity.TaskCode, entity.Title, entity.Description, entity.Status, entity.Priority, entity.AssigneeUserId, entity.AssigneeEmploymentId, entity.StartDate, entity.DueDate, entity.ProgressPercent, entity.Weight, entity.EstimateMinutes, entity.ActualMinutes, entity.SortOrder, entity.Depth, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime, IsOverdue: ProjectManagementDomainRules.IsTaskOverdue(entity.Status, entity.DueDate, DateTime.UtcNow), Summary: entity.Summary, Markdown: entity.Description);
}

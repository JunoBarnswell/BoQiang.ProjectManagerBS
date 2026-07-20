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
    ProjectManagementTaskStateMachine? taskStateMachine = null,
    IProjectManagementReversibleCommandWriter? reversibleCommandWriter = null,
    ProjectManagementWipCoordinator? wipCoordinator = null,
    IProjectManagementNotificationPublisher? notificationPublisher = null,
    IProjectManagementDisplayProjectionService? displayProjection = null,
    IProjectManagementTaskDraftService? draftService = null) : IProjectManagementTaskService, IProjectManagementTaskOccurrenceCommandService, IProjectManagementTaskTemplateCommandService
{
    private static readonly string[] Priorities = ["Low", "Medium", "High", "Urgent"];

    public async Task<GridPageResult<ProjectManagementTaskListItemResponse>> QueryAsync(ProjectManagementTaskQuery query, CancellationToken cancellationToken = default)
    {
        query = ProjectManagementTaskQueryProtocol.Normalize(query);
        await EnsureProjectAsync(query.ProjectId, cancellationToken);
        await AccessPolicy.EnsureCanViewProjectAsync(query.ProjectId, cancellationToken);
        var keyword = NormalizeOptional(query.Keyword);
        var status = NormalizeOptional(query.Status);
        var assignee = NormalizeOptional(query.AssigneeUserId);
        var workItemType = NormalizeOptional(query.WorkItemType);
        var riskLevel = NormalizeOptional(query.RiskLevel);
        var requirementType = NormalizeOptional(query.RequirementType);
        var requirementSource = NormalizeOptional(query.RequirementSource);
        var mentionedUserId = NormalizeOptional(query.MentionedUserId);
        var labelFilter = ProjectManagementTaskLabelFilterQuery.Normalize(query.LabelFilter);
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var taskQuery = databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == query.ProjectId && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(keyword))
            taskQuery = taskQuery.Where(item => item.TaskCode.Contains(keyword) || item.Title.Contains(keyword) ||
                (item.Summary != null && item.Summary.Contains(keyword)) || (item.Description != null && item.Description.Contains(keyword)));
        if (!string.IsNullOrWhiteSpace(status))
            taskQuery = taskQuery.Where(item => item.Status == status);
        if (!string.IsNullOrWhiteSpace(assignee))
            taskQuery = taskQuery.Where(item => item.AssigneeUserId == assignee);
        if (!string.IsNullOrWhiteSpace(workItemType))
            taskQuery = taskQuery.Where(item => item.WorkItemType == workItemType);
        if (!string.IsNullOrWhiteSpace(riskLevel))
            taskQuery = taskQuery.Where(item => item.RiskLevel == riskLevel);
        if (!string.IsNullOrWhiteSpace(requirementType))
            taskQuery = taskQuery.Where(item => item.RequirementType == requirementType);
        if (!string.IsNullOrWhiteSpace(requirementSource))
            taskQuery = taskQuery.Where(item => item.RequirementSource == requirementSource);
        if (!string.IsNullOrWhiteSpace(mentionedUserId))
            taskQuery = taskQuery.Where(item => item.MentionUserIdsJson != null && item.MentionUserIdsJson.Contains($"\"{mentionedUserId}\""));
        if (query.HasChildren is true)
            taskQuery = taskQuery.Where(item => databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Any(child => child.ParentTaskId == item.Id && !child.IsDeleted));
        if (query.HasChildren is false)
            taskQuery = taskQuery.Where(item => !databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Any(child => child.ParentTaskId == item.Id && !child.IsDeleted));
        if (query.HasAttachment is true)
            taskQuery = taskQuery.Where(item => databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskAttachmentEntity>().Any(attachment => attachment.TaskId == item.Id && !attachment.IsDeleted));
        if (query.HasAttachment is false)
            taskQuery = taskQuery.Where(item => !databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskAttachmentEntity>().Any(attachment => attachment.TaskId == item.Id && !attachment.IsDeleted));
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
        var parentIdsWithChildren = ids.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
                .Where(item => item.ProjectId == query.ProjectId && item.ParentTaskId != null && ids.Contains(item.ParentTaskId) && !item.IsDeleted)
                .Select(item => item.ParentTaskId)
                .ToListAsync(cancellationToken))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToHashSet(StringComparer.Ordinal);
        var childRows = ids.Count == 0 ? [] : await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == query.ProjectId && item.ParentTaskId != null && ids.Contains(item.ParentTaskId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var childCounts = childRows.GroupBy(item => item.ParentTaskId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var attachmentTaskIds = ids.Count == 0 ? new HashSet<string>(StringComparer.Ordinal) : (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskAttachmentEntity>()
            .Where(item => item.ProjectId == query.ProjectId && ids.Contains(item.TaskId) && !item.IsDeleted)
            .Select(item => item.TaskId)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var taskLabelLinks = ids.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskLabelEntity>()
                .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.ProjectId == query.ProjectId && ids.Contains(item.TaskId) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var labelIds = taskLabelLinks.Select(item => item.LabelId).Distinct(StringComparer.Ordinal).ToList();
        var labelRows = labelIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementLabelEntity>()
                .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted && labelIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
        var labelsById = labelRows.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var labelsByTask = taskLabelLinks
            .Where(item => labelsById.ContainsKey(item.LabelId))
            .GroupBy(item => item.TaskId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProjectManagementTaskLabelResponse>)group.Select(item =>
                {
                    var label = labelsById[item.LabelId];
                    return new ProjectManagementTaskLabelResponse(item.Id, item.TaskId, label.Id, label.LabelName, label.Color);
                }).ToList(),
                StringComparer.Ordinal);
        var participantRows = ids.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskParticipantEntity>()
                .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.ProjectId == query.ProjectId && ids.Contains(item.TaskId) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var participantUserIdsByTask = participantRows
            .GroupBy(item => item.TaskId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(item => item.UserId).Distinct(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        var displayUserIds = items.Select(item => item.AssigneeUserId)
            .Concat(participantRows.Select(item => item.UserId))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var displays = displayUserIds.Length == 0
            ? null
            : await DisplayProjection.ResolveAsync([], [], displayUserIds, cancellationToken);
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
                return MapList(
                    item,
                    blockers.Count,
                    blockers.Count == 0 || forceStarted,
                    blockers.Count == 0 || forceStarted ? item.BlockedReason : $"存在 {blockers.Count} 个未完成前置任务",
                    parentIdsWithChildren.Contains(item.Id),
                    labelsByTask.GetValueOrDefault(item.Id),
                    participantUserIdsByTask.GetValueOrDefault(item.Id),
                    displays?.User(item.AssigneeUserId),
                    participantUserIdsByTask.GetValueOrDefault(item.Id)?.Select(userId => displays?.User(userId) ?? "用户别名暂不可用").ToList(),
                    childCounts.GetValueOrDefault(item.Id)?.Count ?? 0,
                    childCounts.GetValueOrDefault(item.Id)?.Count(child => child.Status == ProjectManagementDomainRules.TaskDone) ?? 0,
                    attachmentTaskIds.Contains(item.Id));
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
        var taskCode = await ResolveCreateTaskCodeAsync(db, projectId, request.TaskCode, cancellationToken);
        if (await db.Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.ProjectId == projectId && item.TaskCode == taskCode && !item.IsDeleted, cancellationToken))
            throw new ValidationException("项目内任务编码已存在");
        var placement = await TaskHierarchy.ResolvePlacementAsync(db, projectId, request.ParentTaskId, null, cancellationToken);
        var parent = placement.Parent;
        await EnsureTaskWriteAccessAsync(projectId, null, parent?.Id, request.AssigneeUserId, cancellationToken);
        await EnsureAssigneeAsync(projectId, request.AssigneeUserId, cancellationToken);
        var depth = placement.RootDepth;
        var state = StateMachine.Resolve(string.Empty, request.Status, request.ProgressPercent, null, null, now: DateTime.UtcNow, isNew: true);
        await EnsureMilestoneAsync(projectId, request.MilestoneId, cancellationToken);
        await using var wipLease = state.Status == ProjectManagementDomainRules.TaskInProgress
            ? await WipCoordinator.EnterAsync(RequireTenantId(), RequireAppCode(), projectId, cancellationToken)
            : null;
        var now = DateTime.UtcNow;
        var weight = ResolveProgressWeight(request.Weight, request.EstimateMinutes);
        var entity = new ProjectManagementTaskEntity
        {
            TenantId = RequireTenantId(), AppCode = RequireAppCode(), ProjectId = projectId,
            MilestoneId = NormalizeOptional(request.MilestoneId), ParentTaskId = parent?.Id,
            TaskCode = taskCode, Title = NormalizeRequired(request.Title, "任务标题不能为空"), Summary = NormalizeOptional(request.Summary), Description = ResolveMarkdown(request),
            WorkItemType = NormalizeWorkItemType(request.WorkItemType), ContentJson = NormalizeOptional(request.ContentJson), ContentText = NormalizeOptional(request.ContentText), MentionUserIdsJson = SerializeIds(request.MentionUserIds),
            Status = state.Status, BlockedReason = state.Status == ProjectManagementDomainRules.TaskBlocked ? "手工阻塞" : null, Priority = NormalizePriority(request.Priority), AssigneeUserId = NormalizeOptional(request.AssigneeUserId),
            RiskLevel = NormalizeRiskLevel(request.RiskLevel), RequirementType = NormalizeOptional(request.RequirementType), RequirementSource = NormalizeOptional(request.RequirementSource), StoryPoints = request.StoryPoints,
            AssigneeEmploymentId = NormalizeOptional(request.AssigneeEmploymentId), StartDate = request.StartDate, DueDate = request.DueDate,
            ActualStartAt = state.ActualStartAt, ActualEndAt = state.ActualEndAt, ProgressPercent = state.ProgressPercent, Weight = weight, EstimateMinutes = request.EstimateMinutes,
            SortOrder = await GetNextSiblingSortOrderAsync(projectId, parent?.Id, cancellationToken), Depth = depth, VersionNo = 1, CreatedBy = RequireUserId(), CreatedTime = now
        };
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            var wipOverride = await EnsureWipAsync(db, projectId, state.Status == ProjectManagementDomainRules.TaskInProgress, request.OverrideWip, request.OverrideWipReason, cancellationToken);
            if (recurrenceOccurrence is not null)
            {
                recurrenceOccurrence.State = "Generating";
                await db.Insertable(recurrenceOccurrence).ExecuteCommandAsync(cancellationToken);
            }
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await SyncFollowersAsync(db, entity, request.FollowerUserIds, cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.DraftId) && draftService is not null) await draftService.BindAsync(request.DraftId, entity.Id, projectId, cancellationToken);
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
            await WriteWipOverrideActivityAsync(entity, wipOverride, cancellationToken);
            await WriteSyncJournalAsync(entity, "created", cancellationToken);
        });
        await PublishTaskChangeNotificationsAsync(entity, null, cancellationToken);
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
        await using var wipLease = nodes.Any(node => string.Equals(node.Task.Status, ProjectManagementDomainRules.TaskInProgress, StringComparison.OrdinalIgnoreCase))
            ? await WipCoordinator.EnterAsync(RequireTenantId(), RequireAppCode(), projectId, cancellationToken)
            : null;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            foreach (var node in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parentId = string.IsNullOrWhiteSpace(node.Task.ParentTaskId) ? null : created.GetValueOrDefault(node.Task.ParentTaskId!)?.Id
                    ?? throw new ValidationException("模板父任务节点不存在或排序无效");
                var task = node.Task with { ParentTaskId = parentId };
                var entity = await PrepareTemplateTaskAsync(projectId, task, cancellationToken);
                var wipOverride = await EnsureWipAsync(db, projectId, entity.Status == ProjectManagementDomainRules.TaskInProgress, task.OverrideWip, task.OverrideWipReason, cancellationToken);
                await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
                if (node.LabelIds.Count > 0) await (labelMutation ?? new ProjectManagementTaskLabelMutation()).ReplaceAsync(db, entity, node.LabelIds, RequireTenantId(), RequireAppCode(), RequireUserId(), DateTime.UtcNow, cancellationToken);
                await WriteActivityAsync(entity, "created", $"从模板创建任务 {entity.Title}", cancellationToken);
                await WriteWipOverrideActivityAsync(entity, wipOverride, cancellationToken);
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
        var before = ProjectManagementReversibleCommandHandler.ToUpsert(await MapDetailAsync(entity, cancellationToken));
        Validate(request);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var localValues = ProjectManagementTaskConflictLocalValues.FromUpdate(request);
        var state = StateMachine.Resolve(entity.Status, request.Status, request.ProgressPercent, entity.ActualStartAt, entity.ActualEndAt, DateTime.UtcNow);
        var db = databaseAccessor.GetCurrentDb();
        var placement = await TaskHierarchy.ResolvePlacementAsync(db, entity.ProjectId, request.ParentTaskId, entity.Id, cancellationToken);
        var parent = placement.Parent;
        await EnsureTaskWriteAccessAsync(entity.ProjectId, entity.Id, parent?.Id, request.AssigneeUserId, cancellationToken, requireParentScope: true);
        await EnsureAssigneeAsync(entity.ProjectId, request.AssigneeUserId, cancellationToken);
        var entersInProgress = state.Status == ProjectManagementDomainRules.TaskInProgress && entity.Status != ProjectManagementDomainRules.TaskInProgress;
        if (entersInProgress && dependencyService is not null)
            await dependencyService.EnsureCanStartAsync(entity.ProjectId, entity.Id, cancellationToken);
        await using var wipLease = entersInProgress
            ? await WipCoordinator.EnterAsync(RequireTenantId(), RequireAppCode(), entity.ProjectId, cancellationToken)
            : null;
        if (state.Status == ProjectManagementDomainRules.TaskDone && entity.Status != ProjectManagementDomainRules.TaskDone)
            await AccessPolicy.EnsureCanCompleteTasksAsync(entity.ProjectId, [entity.Id], new HashSet<string>([entity.Id], StringComparer.Ordinal), request.ForceComplete, request.ForceCompleteReason, cancellationToken);
        var depth = placement.RootDepth;
        await EnsureMilestoneAsync(entity.ProjectId, request.MilestoneId, cancellationToken);
        var taskCode = NormalizeOptional(request.TaskCode) ?? entity.TaskCode;
        if (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.ProjectId == entity.ProjectId && item.TaskCode == taskCode && item.Id != entity.Id && !item.IsDeleted, cancellationToken))
            throw new ValidationException("项目内任务编码已存在");
        entity.MilestoneId = NormalizeOptional(request.MilestoneId);
        entity.ParentTaskId = parent?.Id;
        entity.Depth = depth;
        entity.TaskCode = taskCode;
        entity.Title = NormalizeRequired(request.Title, "任务标题不能为空");
        entity.Summary = NormalizeOptional(request.Summary);
        entity.Description = ResolveMarkdown(request);
        entity.WorkItemType = NormalizeWorkItemType(request.WorkItemType);
        entity.ContentJson = NormalizeOptional(request.ContentJson);
        entity.ContentText = NormalizeOptional(request.ContentText);
        entity.MentionUserIdsJson = SerializeIds(request.MentionUserIds);
        entity.Status = state.Status;
        entity.BlockedReason = state.Status == ProjectManagementDomainRules.TaskBlocked ? entity.BlockedReason ?? "手工阻塞" : null;
        entity.Priority = NormalizePriority(request.Priority);
        entity.RiskLevel = NormalizeRiskLevel(request.RiskLevel);
        entity.RequirementType = NormalizeOptional(request.RequirementType);
        entity.RequirementSource = NormalizeOptional(request.RequirementSource);
        entity.StoryPoints = request.StoryPoints;
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
            var wipOverride = await EnsureWipAsync(db, entity.ProjectId, entersInProgress, request.OverrideWip, request.OverrideWipReason, cancellationToken, entity.Id);
            await TaskHierarchy.UpdateDescendantDepthsAsync(
                db, placement, entity.UpdatedTime ?? DateTime.UtcNow, entity.UpdatedBy ?? RequireUserId(), cancellationToken);
            await UpdateTaskWithExpectedVersionAsync(db, entity, expectedVersion, cancellationToken, localValues);
            await SyncFollowersAsync(db, entity, request.FollowerUserIds, cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.DraftId) && draftService is not null) await draftService.BindAsync(request.DraftId, entity.Id, entity.ProjectId, cancellationToken);
            if (dependencyService is not null) await dependencyService.RefreshBlockedStatesAsync(entity.ProjectId, cancellationToken);
            await RefreshProgressProjectionsAsync(entity.ProjectId, cancellationToken);
            await WriteActivityAsync(entity, "updated", $"更新任务 {entity.Title}", cancellationToken);
            await WriteWipOverrideActivityAsync(entity, wipOverride, cancellationToken);
            await WriteSyncJournalAsync(entity, "updated", cancellationToken);
        });
        if (imConversationService is not null)
        {
            await imConversationService.SynchronizeTaskLinksAsync(entity.Id, cancellationToken);
        }
        await PublishTaskChangeNotificationsAsync(entity, before, cancellationToken);
        await PublishInvalidationAsync(entity, "task.updated", cancellationToken, before);
        var result = await MapDetailAsync(entity, cancellationToken);
        var commandType = request.AssigneeUserId != before.AssigneeUserId
            ? ProjectManagementReversibleCommandTypes.TaskAssigneeChanged
            : request.Status != before.Status || request.ProgressPercent != before.ProgressPercent
                ? ProjectManagementReversibleCommandTypes.TaskStatusProgressChanged
                : ProjectManagementReversibleCommandTypes.TaskUpdated;
        await RecordReversibleAsync(commandType, entity.ProjectId, "Task", entity.Id,
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementTaskUpdateCommand(entity.Id, request)),
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementTaskUpdateCommand(entity.Id, before with { VersionNo = result.VersionNo })),
            $"更新任务 {entity.Title}", cancellationToken);
        return result;
    }

    public async Task<ProjectManagementTaskDetailResponse> ChangeStatusAsync(string id, ProjectManagementTaskStatusChangeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var detail = await GetAsync(id, cancellationToken);
        var progress = string.Equals(request.Status, ProjectManagementDomainRules.TaskDone, StringComparison.Ordinal)
            ? 100m
            : string.Equals(detail.Status, ProjectManagementDomainRules.TaskDone, StringComparison.Ordinal)
                ? 0m
                : detail.ProgressPercent;
        var update = ProjectManagementReversibleCommandHandler.ToUpsert(detail) with
        {
            ProgressPercent = progress,
            Status = request.Status,
            VersionNo = request.VersionNo
        };
        return await UpdateAsync(id, update, cancellationToken);
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
        await EnsureTaskWriteAccessAsync(entity.ProjectId, entity.Id, parent?.Id, entity.AssigneeUserId, cancellationToken, requireParentScope: true);
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
        await RecordReversibleAsync(ProjectManagementReversibleCommandTypes.TaskSoftDeleted, entity.ProjectId, "Task", entity.Id,
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementTaskDeleteCommand(entity.Id, request.Mode, request.VersionNo)),
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementTaskRestoreCommand(entity.Id, deleteMode == ProjectManagementTaskHierarchy.CascadeDeleteMode, entity.VersionNo)),
            $"删除任务 {entity.Title}", cancellationToken);
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
        var result = await MapDetailAsync(entity, cancellationToken);
        await RecordReversibleAsync(ProjectManagementReversibleCommandTypes.TaskRestored, entity.ProjectId, "Task", entity.Id,
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementTaskRestoreCommand(entity.Id, false, versionNo)),
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementTaskDeleteCommand(entity.Id, ProjectManagementTaskDeleteModes.Cascade, result.VersionNo)),
            $"恢复任务 {entity.Title}", cancellationToken);
        return result;
    }

    private async Task PublishInvalidationAsync(ProjectManagementTaskEntity entity, string eventType, CancellationToken cancellationToken, ProjectManagementTaskUpsertRequest? before = null)
    {
        if (realtimePublisher is null) return;
        var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        var patch = BuildRealtimeTaskPatch(entity, before, out var changedFields);
        await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(
            RequireTenantId(), RequireAppCode(), "Task", entity.Id, eventType, entity.VersionNo, traceId, entity.ProjectId,
            ChangedFields: changedFields, Patch: patch), cancellationToken);
    }

    private static IReadOnlyDictionary<string, object?> BuildRealtimeTaskPatch(ProjectManagementTaskEntity entity, ProjectManagementTaskUpsertRequest? before, out IReadOnlyList<string> changedFields)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var changed = new List<string>();
        void Add(string field, object? current, object? previous)
        {
            if (before is null || !Equals(current, previous))
            {
                changed.Add(field);
                values[field] = current;
            }
        }

        Add("id", entity.Id, before is null ? null : entity.Id);
        Add("projectId", entity.ProjectId, before is null ? null : entity.ProjectId);
        Add("taskCode", entity.TaskCode, before?.TaskCode);
        Add("title", entity.Title, before?.Title);
        Add("summary", entity.Summary, before?.Summary);
        Add("status", entity.Status, before?.Status);
        Add("priority", entity.Priority, before?.Priority);
        Add("milestoneId", entity.MilestoneId, before?.MilestoneId);
        Add("parentTaskId", entity.ParentTaskId, before?.ParentTaskId);
        Add("assigneeUserId", entity.AssigneeUserId, before?.AssigneeUserId);
        Add("startDate", entity.StartDate?.ToString("yyyy-MM-dd"), before?.StartDate?.ToString("yyyy-MM-dd"));
        Add("dueDate", entity.DueDate?.ToString("yyyy-MM-dd"), before?.DueDate?.ToString("yyyy-MM-dd"));
        Add("progressPercent", entity.ProgressPercent, before?.ProgressPercent);
        Add("workItemType", entity.WorkItemType, before?.WorkItemType);
        Add("contentJson", entity.ContentJson, before?.ContentJson);
        Add("contentText", entity.ContentText, before?.ContentText);
        Add("riskLevel", entity.RiskLevel, before?.RiskLevel);
        Add("requirementType", entity.RequirementType, before?.RequirementType);
        Add("requirementSource", entity.RequirementSource, before?.RequirementSource);
        Add("storyPoints", entity.StoryPoints, before?.StoryPoints);
        var mentionUserIds = DeserializeIds(entity.MentionUserIdsJson);
        if (before is null || !mentionUserIds.SequenceEqual(before.MentionUserIds ?? [], StringComparer.OrdinalIgnoreCase))
        {
            changed.Add("mentionUserIds");
            values["mentionUserIds"] = mentionUserIds;
        }
        Add("versionNo", entity.VersionNo, before?.VersionNo);
        Add("updatedTime", entity.UpdatedTime, null);
        if (before is null || entity.IsDeleted)
        {
            values["isDeleted"] = entity.IsDeleted;
            changed.Add("isDeleted");
        }
        changedFields = changed;
        return values;
    }

    private async Task WriteActivityAsync(ProjectManagementTaskEntity entity, string activityType, string summary, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(RequireTenantId(), RequireAppCode(), "Task", entity.Id, activityType, summary, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), entity.ProjectId), cancellationToken);
    }

    private async Task WriteWipOverrideActivityAsync(ProjectManagementTaskEntity entity, ProjectManagementWipOverrideDecision? decision, CancellationToken cancellationToken)
    {
        if (decision is null || activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            RequireTenantId(), RequireAppCode(), "Task", entity.Id, "task.wip-overridden",
            $"强制超过 WIP 上限开始任务：{decision.Reason}", Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), entity.ProjectId,
            Source: "Governance", FieldChanges:
            [
                new ProjectManagementActivityFieldChange("WipLimit", "WIP 上限", decision.Limit.ToString(), decision.Limit.ToString()),
                new ProjectManagementActivityFieldChange("InProgressCount", "开始前进行中任务数", decision.InProgressCount.ToString(), (decision.InProgressCount + 1).ToString()),
                new ProjectManagementActivityFieldChange("OverrideReason", "强制原因", null, decision.Reason)
            ]), cancellationToken);
    }

    private Task RecordReversibleAsync(string commandType, string projectId, string aggregateType, string aggregateId, string forwardJson, string inverseJson, string summary, CancellationToken cancellationToken)
    {
        if (reversibleCommandWriter is null || ProjectManagementReversibleCommandReplayScope.IsActive) return Task.CompletedTask;
        return reversibleCommandWriter.TryRecordCommittedAsync(ProjectManagementReversibleCommandCapability.Instance,
            new ProjectManagementReversibleCommandRecordRequest(
                Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), commandType, projectId, aggregateType, aggregateId,
                forwardJson, inverseJson, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), summary), cancellationToken);
    }

    private async Task WriteSyncJournalAsync(ProjectManagementTaskEntity entity, string operation, CancellationToken cancellationToken)
    {
        if (syncJournalWriter is null) return;
        await syncJournalWriter.AppendAsync(new ProjectManagementSyncJournalEvent(
            RequireTenantId(), RequireAppCode(), "Task", entity.Id, entity.ProjectId, operation, entity.VersionNo,
            JsonSerializer.Serialize(entity), RequireUserId(), null, Activity.Current?.Id ?? Guid.NewGuid().ToString("N")), cancellationToken);
    }

    private ProjectManagementAccessPolicy AccessPolicy => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private IProjectManagementDisplayProjectionService DisplayProjection => displayProjection ?? new ProjectManagementDisplayProjectionService(databaseAccessor);
    private ProjectManagementWipCoordinator WipCoordinator => wipCoordinator ?? new ProjectManagementWipCoordinator();
    private ProjectManagementTaskHierarchy TaskHierarchy => taskHierarchy ?? new ProjectManagementTaskHierarchy();
    private ProjectManagementTaskStateMachine StateMachine => taskStateMachine ?? new ProjectManagementTaskStateMachine();

    // 保持任务写入授权在单一接缝：ScopeRootTaskId 权限落地后只需在此转发任务与父任务上下文。
    private Task EnsureTaskWriteAccessAsync(string projectId, string? taskId, string? parentTaskId, string? assigneeUserId, CancellationToken cancellationToken, bool requireParentScope = false)
        => AccessPolicy.EnsureCanManageTaskAsync(projectId, taskId, parentTaskId, assigneeUserId, requireParentScope, cancellationToken);

    private async Task EnsureProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .AnyAsync(cancellationToken))
            throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private static async Task<string> ResolveCreateTaskCodeAsync(ISqlSugarClient db, string projectId, string? requestedTaskCode, CancellationToken cancellationToken)
    {
        var normalized = NormalizeOptional(requestedTaskCode);
        if (normalized is not null) return normalized;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var generated = $"REQ-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
            if (!await db.Queryable<ProjectManagementTaskEntity>()
                    .Where(item => item.ProjectId == projectId && item.TaskCode == generated && !item.IsDeleted)
                    .AnyAsync(cancellationToken))
                return generated;
        }

        throw new ValidationException("无法生成唯一任务编码，请稍后重试");
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

    private async Task UpdateTaskWithExpectedVersionAsync(
        ISqlSugarClient db,
        ProjectManagementTaskEntity entity,
        long expectedVersion,
        CancellationToken cancellationToken,
        ProjectManagementTaskConflictLocalValues? localValues = null)
    {
        if (await db.Updateable(entity)
                .Where(item => item.Id == entity.Id && item.VersionNo == expectedVersion)
                .ExecuteCommandAsync(cancellationToken) == 1)
            return;

        var server = (await db.Queryable<ProjectManagementTaskEntity>()
                .Where(item => item.Id == entity.Id)
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (server is null || server.IsDeleted)
            throw new NotFoundException("任务已不存在", ErrorCodes.PlatformResourceNotFound);

        if (localValues is null)
            throw new ValidationException("任务已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);

        var serverValues = await MapDetailAsync(server, cancellationToken);
        throw new ProjectManagementTaskVersionConflictException(
            new ProjectManagementTaskVersionConflictResponse(
                serverValues,
                localValues,
                CreateVersionConflictFields(serverValues, localValues)));
    }

    private static IReadOnlyList<ProjectManagementTaskConflictField> CreateVersionConflictFields(
        ProjectManagementTaskDetailResponse server,
        ProjectManagementTaskConflictLocalValues local)
    {
        var fields = new List<ProjectManagementTaskConflictField>();
        foreach (var field in local.SubmittedFields)
        {
            var conflict = field switch
            {
                "VersionNo" => CreateVersionConflictField(field, "版本号", server.VersionNo, local.VersionNo),
                "TaskCode" => CreateVersionConflictField(field, "任务编码", server.TaskCode, local.TaskCode),
                "Title" => CreateVersionConflictField(field, "任务标题", server.Title, local.Title),
                "Description" => CreateVersionConflictField(field, "描述", server.Description, local.Description),
                "Status" => CreateVersionConflictField(field, "状态", server.Status, local.Status),
                "Priority" => CreateVersionConflictField(field, "优先级", server.Priority, local.Priority),
                "MilestoneId" => CreateVersionConflictField(field, "里程碑", server.MilestoneId, local.MilestoneId),
                "ParentTaskId" => CreateVersionConflictField(field, "父任务", server.ParentTaskId, local.ParentTaskId),
                "AssigneeUserId" => CreateVersionConflictField(field, "负责人", server.AssigneeUserId, local.AssigneeUserId),
                "AssigneeEmploymentId" => CreateVersionConflictField(field, "负责人任职", server.AssigneeEmploymentId, local.AssigneeEmploymentId),
                "StartDate" => CreateVersionConflictField(field, "开始日期", server.StartDate, local.StartDate),
                "DueDate" => CreateVersionConflictField(field, "截止日期", server.DueDate, local.DueDate),
                "ProgressPercent" => CreateVersionConflictField(field, "进度", server.ProgressPercent, local.ProgressPercent),
                "Weight" => CreateVersionConflictField(field, "进度权重", server.Weight, local.Weight),
                "EstimateMinutes" => CreateVersionConflictField(field, "预计工时", server.EstimateMinutes, local.EstimateMinutes),
                "Markdown" => CreateVersionConflictField(field, "Markdown", server.Markdown, local.Markdown),
                "Summary" => CreateVersionConflictField(field, "摘要", server.Summary, local.Summary),
                _ => null
            };
            if (conflict is not null) fields.Add(conflict);
        }
        return fields;
    }

    private static ProjectManagementTaskConflictField? CreateVersionConflictField(string field, string displayName, object? serverValue, object? localValue) =>
        Equals(serverValue, localValue) ? null : new ProjectManagementTaskConflictField(field, displayName, serverValue, localValue);

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
        return new ProjectManagementTaskEntity
        {
            TenantId = RequireTenantId(), AppCode = RequireAppCode(), ProjectId = projectId, MilestoneId = NormalizeOptional(request.MilestoneId), ParentTaskId = parent?.Id,
            TaskCode = taskCode, Title = NormalizeRequired(request.Title, "任务标题不能为空"), Summary = NormalizeOptional(request.Summary), Description = ResolveMarkdown(request), Status = state.Status,
            BlockedReason = state.Status == ProjectManagementDomainRules.TaskBlocked ? "手工阻塞" : null, Priority = NormalizePriority(request.Priority), AssigneeUserId = NormalizeOptional(request.AssigneeUserId), AssigneeEmploymentId = NormalizeOptional(request.AssigneeEmploymentId),
            StartDate = request.StartDate, DueDate = request.DueDate, ActualStartAt = state.ActualStartAt, ActualEndAt = state.ActualEndAt, ProgressPercent = state.ProgressPercent, Weight = ResolveProgressWeight(request.Weight, request.EstimateMinutes), EstimateMinutes = request.EstimateMinutes,
            SortOrder = await GetNextSiblingSortOrderAsync(projectId, parent?.Id, cancellationToken), Depth = placement.RootDepth, VersionNo = 1, CreatedBy = RequireUserId(), CreatedTime = DateTime.UtcNow
        };
    }

    private async Task<ProjectManagementWipOverrideDecision?> EnsureWipAsync(ISqlSugarClient db, string projectId, bool entersInProgress, bool overrideWip, string? overrideReason, CancellationToken cancellationToken, string? currentTaskId = null)
    {
        if (!entersInProgress) return null;
        var project = await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        var limit = project.FirstOrDefault()?.WipLimit;
        if (!limit.HasValue) return null;
        var count = await db.Queryable<ProjectManagementTaskEntity>().CountAsync(item => item.ProjectId == projectId && item.Status == ProjectManagementDomainRules.TaskInProgress && !item.IsDeleted && item.Id != (currentTaskId ?? string.Empty), cancellationToken);
        if (count < limit.Value) return null;
        if (!overrideWip) throw new ValidationException("项目 WIP 上限已达到，需要 WIP 强制绕过权限");
        if (!currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementTaskOverrideWip))
            throw new ValidationException("没有 WIP 强制绕过权限", ErrorCodes.PermissionDenied);
        return new ProjectManagementWipOverrideDecision(limit.Value, count, NormalizeWipOverrideReason(overrideReason));
    }

    private static void Validate(ProjectManagementTaskUpsertRequest request)
    {
        ProjectManagementDomainRules.ValidateDates(request.StartDate, request.DueDate, "任务");
        ProjectManagementDomainRules.RequireProgress(request.ProgressPercent, "任务");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 256) throw new ValidationException("任务标题不能为空且不能超过 256 个字符");
        if (request.Weight is <= 0) throw new ValidationException("任务权重必须大于 0");
        if (request.EstimateMinutes is < 0) throw new ValidationException("预估工时不能为负数");
        if (request.StoryPoints is < 0) throw new ValidationException("故事点不能为负数");
        _ = NormalizeWorkItemType(request.WorkItemType);
        _ = NormalizeRiskLevel(request.RiskLevel);
        _ = ResolveMarkdown(request);
    }

    private static decimal ResolveProgressWeight(decimal? explicitWeight, int? estimateMinutes) =>
        explicitWeight ?? (estimateMinutes is > 0 ? estimateMinutes.Value : 1m);

    private static string NormalizePriority(string value) => Priorities.Contains(value.Trim(), StringComparer.Ordinal) ? value.Trim() : throw new ValidationException("任务优先级不受支持");
    private static string NormalizeWorkItemType(string? value) => value?.Trim() switch
    {
        "Epic" or "Story" or "Requirement" or "Task" or "Bug" => value.Trim(),
        null or "" => "Task",
        _ => throw new ValidationException("工作项类型不受支持")
    };
    private static string NormalizeRiskLevel(string? value) => value?.Trim() switch
    {
        "None" or "Low" or "Medium" or "High" or "Closed" => value.Trim(),
        null or "" => "None",
        _ => throw new ValidationException("风险等级不受支持")
    };
    private static bool IsForceStarted(string? blockedReason) => blockedReason?.StartsWith("已强制开始：", StringComparison.Ordinal) == true;
    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string NormalizeRequired(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeWipOverrideReason(string? value)
    {
        var reason = value?.Trim() ?? string.Empty;
        if (reason.Length is < 1 or > 500) throw new ValidationException("WIP 强制绕过原因必须在 1 到 500 个字符之间");
        return reason;
    }
    private static string? ResolveMarkdown(ProjectManagementTaskUpsertRequest request)
    {
        var legacyDescription = ProjectManagementMarkdownPolicy.NormalizeOptional(request.Description);
        var markdown = ProjectManagementMarkdownPolicy.NormalizeOptional(request.Markdown);
        if (legacyDescription is not null && markdown is not null && !string.Equals(legacyDescription, markdown, StringComparison.Ordinal))
            throw new ValidationException("Description 与 Markdown 不能同时提供不同内容");
        return markdown ?? legacyDescription;
    }
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("任务已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private sealed record ProjectManagementWipOverrideDecision(int Limit, int InProgressCount, string Reason);
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
        var children = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ParentTaskId == entity.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        var hasAttachments = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskAttachmentEntity>()
            .Where(item => item.TaskId == entity.Id && !item.IsDeleted).AnyAsync(cancellationToken);
        var followerIds = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskFollowerEntity>()
            .Where(item => item.TaskId == entity.Id && !item.IsDeleted).Select(item => item.UserId).ToListAsync(cancellationToken);
        var capabilities = await AccessPolicy.GetTaskAccessCapabilitiesAsync(entity.ProjectId, entity.Id, entity.AssigneeUserId, cancellationToken);
        return MapDetail(
            entity,
            blockedByCount,
            blockedByCount == 0 || forceStarted,
            blockedByCount == 0 || forceStarted ? entity.BlockedReason : $"存在 {blockedByCount} 个未完成前置任务",
            children.Count,
            children.Count(item => item.Status == ProjectManagementDomainRules.TaskDone),
            hasAttachments,
            DeserializeIds(entity.MentionUserIdsJson),
            followerIds,
            new ProjectManagementTaskAccessCapabilitiesResponse(
                capabilities.CanEdit,
                capabilities.CanComment,
                capabilities.IsReadOnlyGrant));
    }

    private static ProjectManagementTaskListItemResponse MapList(ProjectManagementTaskEntity entity, int blockedByCount, bool canStart, string? blockedReason, bool hasChildren, IReadOnlyList<ProjectManagementTaskLabelResponse>? labels, IReadOnlyList<string>? participantUserIds, string? assigneeDisplayName, IReadOnlyList<string>? participantDisplayNames, int childCount = 0, int completedChildCount = 0, bool hasAttachments = false) => new(entity.Id, entity.ProjectId, entity.MilestoneId, entity.ParentTaskId, entity.TaskCode, entity.Title, entity.Status, entity.Priority, entity.AssigneeUserId, entity.StartDate, entity.DueDate, entity.ProgressPercent, entity.SortOrder, entity.Depth, entity.VersionNo, blockedByCount, canStart, blockedReason, ProjectManagementDomainRules.IsTaskOverdue(entity.Status, entity.DueDate, DateTime.UtcNow), entity.ActualStartAt, entity.ActualEndAt, entity.Summary, hasChildren, labels, participantUserIds, assigneeDisplayName, participantDisplayNames, entity.WorkItemType, entity.RiskLevel, entity.RequirementType, entity.RequirementSource, entity.StoryPoints, childCount, completedChildCount, hasAttachments);

    private static ProjectManagementTaskDetailResponse MapDetail(ProjectManagementTaskEntity entity, int blockedByCount, bool canStart, string? blockedReason, int childCount = 0, int completedChildCount = 0, bool hasAttachments = false, IReadOnlyList<string>? mentionUserIds = null, IReadOnlyList<string>? followerUserIds = null, ProjectManagementTaskAccessCapabilitiesResponse? access = null) => new(entity.Id, entity.ProjectId, entity.MilestoneId, entity.ParentTaskId, entity.TaskCode, entity.Title, entity.Description, entity.Status, entity.Priority, entity.AssigneeUserId, entity.AssigneeEmploymentId, entity.StartDate, entity.DueDate, entity.ProgressPercent, entity.Weight, entity.EstimateMinutes, entity.ActualMinutes, entity.SortOrder, entity.Depth, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime, blockedByCount, canStart, blockedReason, ProjectManagementDomainRules.IsTaskOverdue(entity.Status, entity.DueDate, DateTime.UtcNow), entity.ActualStartAt, entity.ActualEndAt, Summary: entity.Summary, Markdown: entity.Description, WorkItemType: entity.WorkItemType, ContentJson: entity.ContentJson, ContentText: entity.ContentText, RiskLevel: entity.RiskLevel, RequirementType: entity.RequirementType, RequirementSource: entity.RequirementSource, StoryPoints: entity.StoryPoints, MentionUserIds: mentionUserIds, FollowerUserIds: followerUserIds, ChildCount: childCount, CompletedChildCount: completedChildCount, HasAttachments: hasAttachments, Access: access);
    private static ProjectManagementTaskResponse ToTemplateResponse(ProjectManagementTaskEntity entity) => new(entity.Id, entity.ProjectId, entity.MilestoneId, entity.ParentTaskId, entity.TaskCode, entity.Title, entity.Description, entity.Status, entity.Priority, entity.AssigneeUserId, entity.AssigneeEmploymentId, entity.StartDate, entity.DueDate, entity.ProgressPercent, entity.Weight, entity.EstimateMinutes, entity.ActualMinutes, entity.SortOrder, entity.Depth, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime, IsOverdue: ProjectManagementDomainRules.IsTaskOverdue(entity.Status, entity.DueDate, DateTime.UtcNow), Summary: entity.Summary, Markdown: entity.Description);

    private static IReadOnlyList<string> DeserializeIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string? SerializeIds(IReadOnlyList<string>? values)
    {
        if (values is null) return null;
        var normalized = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList();
        return normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized);
    }

    private async Task SyncFollowersAsync(ISqlSugarClient db, ProjectManagementTaskEntity task, IReadOnlyList<string>? requestedUserIds, CancellationToken cancellationToken)
    {
        if (requestedUserIds is null) return;
        var desired = requestedUserIds.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (desired.Count > 0)
        {
            var validMemberIds = (await db.Queryable<ProjectManagementProjectMemberEntity>()
                .Where(member => member.ProjectId == task.ProjectId && member.TenantId == task.TenantId && member.AppCode == task.AppCode && member.IsActive && !member.IsDeleted)
                .Select(member => member.UserId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (desired.Any(userId => !validMemberIds.Contains(userId)) && !currentUser.IsAsterErpPlatformAdmin() && !currentUser.HasAsterErpPermission("*"))
                throw new ValidationException("关注人必须是项目成员", ErrorCodes.PermissionDenied);
        }
        var existing = await db.Queryable<ProjectManagementTaskFollowerEntity>()
            .Where(item => item.TaskId == task.Id && item.TenantId == task.TenantId && item.AppCode == task.AppCode).ToListAsync(cancellationToken);
        foreach (var item in existing)
        {
            var shouldBeActive = desired.Contains(item.UserId);
            if (item.IsDeleted == !shouldBeActive)
            {
                if (shouldBeActive) desired.Remove(item.UserId);
                continue;
            }
            item.IsDeleted = !shouldBeActive;
            item.DeletedBy = shouldBeActive ? null : RequireUserId();
            item.DeletedTime = shouldBeActive ? null : DateTime.UtcNow;
            item.UpdatedBy = RequireUserId();
            item.UpdatedTime = DateTime.UtcNow;
            item.VersionNo++;
            await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
            desired.Remove(item.UserId);
        }
        foreach (var userId in desired)
        {
            await db.Insertable(new ProjectManagementTaskFollowerEntity
            {
                TenantId = task.TenantId, AppCode = task.AppCode, ProjectId = task.ProjectId, TaskId = task.Id, UserId = userId,
                VersionNo = 1, CreatedBy = RequireUserId(), CreatedTime = DateTime.UtcNow,
            }).ExecuteCommandAsync(cancellationToken);
        }
    }

    private async Task PublishTaskChangeNotificationsAsync(ProjectManagementTaskEntity task, ProjectManagementTaskUpsertRequest? before, CancellationToken cancellationToken)
    {
        if (notificationPublisher is null) return;

        var actorUserId = RequireUserId();
        var assigneeChanged = before is null || !string.Equals(NormalizeOptional(before.AssigneeUserId), task.AssigneeUserId, StringComparison.Ordinal);
        if (assigneeChanged && !string.IsNullOrWhiteSpace(task.AssigneeUserId) && !string.Equals(task.AssigneeUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            await PublishNotificationAsync(task, task.AssigneeUserId, ProjectManagementNotificationTypes.TaskAssigned, "任务已分配给你", $"你已被分配处理任务 {task.Title}", cancellationToken);

        if (before is null) return;
        var recipients = await LoadTaskNotificationRecipientsAsync(task, actorUserId, cancellationToken);
        if (!string.Equals(before.Status, task.Status, StringComparison.Ordinal))
        {
            foreach (var recipient in recipients)
                await PublishNotificationAsync(task, recipient, ProjectManagementNotificationTypes.TaskStatusChanged, "任务状态已变更", $"任务 {task.Title} 的状态已变更为 {task.Status}", cancellationToken);
        }
        if (before.DueDate != task.DueDate)
        {
            var dueDate = task.DueDate?.ToString("yyyy-MM-dd") ?? "未设置";
            foreach (var recipient in recipients)
                await PublishNotificationAsync(task, recipient, ProjectManagementNotificationTypes.TaskDueDateChanged, "任务截止日期已变更", $"任务 {task.Title} 的截止日期已变更为 {dueDate}", cancellationToken);
        }
    }

    private async Task<IReadOnlyList<string>> LoadTaskNotificationRecipientsAsync(ProjectManagementTaskEntity task, string actorUserId, CancellationToken cancellationToken)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(task.AssigneeUserId)) recipients.Add(task.AssigneeUserId);
        var participants = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskParticipantEntity>()
            .Where(item => item.TenantId == task.TenantId && item.AppCode == task.AppCode && item.ProjectId == task.ProjectId && item.TaskId == task.Id && !item.IsDeleted)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        recipients.UnionWith(participants);
        recipients.Remove(actorUserId);
        return recipients.OrderBy(item => item, StringComparer.Ordinal).ToList();
    }

    private Task PublishNotificationAsync(ProjectManagementTaskEntity task, string recipientUserId, string notificationType, string title, string message, CancellationToken cancellationToken)
    {
        var traceId = $"notification:{notificationType}:{task.Id}:{task.VersionNo}:{recipientUserId}";
        return notificationPublisher!.PublishAsync(new ProjectManagementNotification(
            task.TenantId,
            task.AppCode,
            notificationType,
            recipientUserId,
            title,
            message,
            $"/projects/{task.ProjectId}/tasks?selectedTaskId={task.Id}",
            traceId,
            task.ProjectId,
            task.Id), cancellationToken);
    }
}

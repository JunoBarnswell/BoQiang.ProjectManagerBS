using System.Text.Json;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiTaskPlanService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    AiConversationService conversationService,
    AiTaskPlanValidator validator,
    AiTaskPlanGuard guard,
    AiTaskPlanEventWriter eventWriter,
    AiPlanParser parser) : IAiTaskPlanService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AiTaskPlanDto>> GetByConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        _ = await conversationService.RequireConversationAsync(conversationId, cancellationToken);
        var plans = await db.Queryable<AiTaskPlanEntity>()
            .Where(item => !item.IsDeleted && item.ConversationId == conversationId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        if (plans.Count == 0)
        {
            return [];
        }

        var planIds = plans.Select(item => item.Id).ToArray();
        var items = await db.Queryable<AiTaskPlanItemEntity>()
            .Where(item => !item.IsDeleted && planIds.Contains(item.PlanId))
            .OrderBy(item => item.Depth)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        return plans.Select(plan => AiTaskPlanMapper.MapPlan(plan, items.Where(item => item.PlanId == plan.Id))).ToList();
    }

    public async Task<AiTaskPlanDto> GetDetailAsync(string planId, bool includeEvents = false, CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(planId, cancellationToken);
        var items = await LoadItemsAsync(plan.Id, cancellationToken);
        var events = includeEvents
            ? await db.Queryable<AiTaskPlanEventEntity>()
                .Where(item => !item.IsDeleted && item.PlanId == plan.Id)
                .OrderBy(item => item.Seq)
                .Take(1000)
                .ToListAsync(cancellationToken)
            : [];
        return AiTaskPlanMapper.MapPlan(plan, items, events);
    }

    public async Task<GridPageResult<AiTaskPlanEventDto>> GetEventsAsync(string planId, long? afterSeq, int pageSize, CancellationToken cancellationToken = default)
    {
        _ = await RequirePlanAsync(planId, cancellationToken);
        var size = Math.Clamp(pageSize <= 0 ? 100 : pageSize, 1, 1000);
        var query = db.Queryable<AiTaskPlanEventEntity>()
            .Where(item => !item.IsDeleted && item.PlanId == planId);
        if (afterSeq.HasValue)
        {
            query = query.Where(item => item.Seq > afterSeq.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.Seq).Take(size).ToListAsync(cancellationToken);
        return new GridPageResult<AiTaskPlanEventDto> { Total = total, Items = rows.Select(AiTaskPlanMapper.MapEvent).ToList() };
    }

    public async Task<GridPageResult<AiTaskPlanItemOutputDto>> GetOutputsAsync(string planId, string? itemId, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        _ = await RequirePlanAsync(planId, cancellationToken);
        var page = Math.Max(pageIndex, 1);
        var size = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200);
        var query = db.Queryable<AiTaskPlanItemOutputEntity>()
            .Where(item => !item.IsDeleted && item.PlanId == planId);
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            var targetItemId = itemId.Trim();
            query = query.Where(item => item.ItemId == targetItemId);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        return new GridPageResult<AiTaskPlanItemOutputDto> { Total = total, Items = rows.Select(AiTaskPlanMapper.MapOutput).ToList() };
    }

    public async Task<AiTaskPlanDto> CreateAsync(string conversationId, AiTaskPlanUpsertRequest request, string? runId = null, CancellationToken cancellationToken = default)
    {
        validator.ValidateUpsert(request, requireItems: true);
        var workspace = workspaceContext.Resolve();
        var conversation = await conversationService.RequireConversationAsync(conversationId, cancellationToken);
        var plan = new AiTaskPlanEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ConversationId = conversation.Id,
            RunId = runId,
            Title = AiTaskPlanValueNormalizer.Required(request.Title, "计划标题不能为空"),
            Goal = AiTaskPlanValueNormalizer.Required(request.Goal, "计划目标不能为空"),
            Status = AiTaskPlanValueNormalizer.PlanStatus(request.Status),
            Mode = AiTaskPlanValueNormalizer.Mode(request.Mode),
            ExecutionStrategy = AiTaskPlanValueNormalizer.ExecutionStrategy(request.ExecutionStrategy),
            RisksJson = AiTaskPlanValueNormalizer.Optional(request.RisksJson),
            AssumptionsJson = AiTaskPlanValueNormalizer.Optional(request.AssumptionsJson),
            MetadataJson = AiTaskPlanValueNormalizer.Optional(request.MetadataJson)
        };
        var items = BuildItemEntities(workspace, conversation.Id, plan.Id, request.Items, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        db.Ado.BeginTran();
        try
        {
            await db.Insertable(plan).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(items).ExecuteCommandAsync(cancellationToken);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }

        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.PlanSaved, summary: "计划已创建", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapPlan(plan, items);
    }

    public async Task<AiTaskPlanDto> CreateFromAssistantContentAsync(AiConversationEntity conversation, string runId, string content, CancellationToken cancellationToken = default)
    {
        var request = parser.Parse(content);
        return await CreateAsync(conversation.Id, request, runId, cancellationToken);
    }

    public async Task<AiTaskPlanDto> UpdateAsync(string planId, AiTaskPlanUpsertRequest request, CancellationToken cancellationToken = default)
    {
        validator.ValidateUpsert(request, requireItems: true);
        var plan = await RequirePlanAsync(planId, cancellationToken);
        guard.EnsureStructureEditable(plan.Status);
        validator.EnsureRevision(request.ExpectedRevision, plan.Revision);
        var existingItems = await LoadItemsAsync(plan.Id, cancellationToken);
        var existingIds = existingItems.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var workspace = new AiWorkspace(plan.TenantId, plan.AppCode, plan.OwnerUserId);
        var replacementItems = BuildItemEntities(workspace, plan.ConversationId, plan.Id, request.Items, existingIds);

        db.Ado.BeginTran();
        try
        {
            plan.Title = AiTaskPlanValueNormalizer.Required(request.Title, "计划标题不能为空");
            plan.Goal = AiTaskPlanValueNormalizer.Required(request.Goal, "计划目标不能为空");
            plan.Status = AiTaskPlanValueNormalizer.PlanStatus(request.Status);
            plan.Mode = AiTaskPlanValueNormalizer.Mode(request.Mode);
            plan.ExecutionStrategy = AiTaskPlanValueNormalizer.ExecutionStrategy(request.ExecutionStrategy);
            plan.RisksJson = AiTaskPlanValueNormalizer.Optional(request.RisksJson);
            plan.AssumptionsJson = AiTaskPlanValueNormalizer.Optional(request.AssumptionsJson);
            plan.MetadataJson = AiTaskPlanValueNormalizer.Optional(request.MetadataJson);
            plan.Revision++;
            plan.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(plan).ExecuteCommandAsync(cancellationToken);
            await SoftDeleteItemsAsync(existingItems, cancellationToken);
            await db.Insertable(replacementItems).ExecuteCommandAsync(cancellationToken);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }

        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.PlanSaved, summary: "计划已保存", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapPlan(plan, replacementItems);
    }

    public async Task<AiTaskPlanDto> DuplicateAsync(string planId, CancellationToken cancellationToken = default)
    {
        var source = await RequirePlanAsync(planId, cancellationToken);
        var sourceItems = await LoadItemsAsync(source.Id, cancellationToken);
        var workspace = workspaceContext.Resolve();
        var copy = new AiTaskPlanEntity
        {
            TenantId = source.TenantId,
            AppCode = source.AppCode,
            OwnerUserId = workspace.UserId,
            ConversationId = source.ConversationId,
            Title = $"{source.Title} 副本",
            Goal = source.Goal,
            Status = AiTaskPlanConstants.PlanStatus.Draft,
            Mode = source.Mode,
            VersionNo = source.VersionNo,
            ExecutionStrategy = source.ExecutionStrategy,
            RisksJson = source.RisksJson,
            AssumptionsJson = source.AssumptionsJson,
            MetadataJson = source.MetadataJson
        };
        var idMap = sourceItems.ToDictionary(item => item.Id, _ => Guid.NewGuid().ToString("N"), StringComparer.OrdinalIgnoreCase);
        var items = sourceItems.Select(item => CloneItem(item, copy.Id, idMap)).ToList();
        await db.Insertable(copy).ExecuteCommandAsync(cancellationToken);
        await db.Insertable(items).ExecuteCommandAsync(cancellationToken);
        await eventWriter.WriteAsync(copy, AiTaskPlanConstants.Event.PlanSaved, summary: "计划已复制", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapPlan(copy, items);
    }

    public async Task DeleteAsync(string planId, CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(planId, cancellationToken);
        guard.EnsureCanCancel(plan.Status);
        var items = await LoadItemsAsync(plan.Id, cancellationToken);
        plan.IsDeleted = true;
        plan.DeletedTime = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.IsDeleted = true;
            item.DeletedTime = DateTime.UtcNow;
        }

        await db.Updateable(plan).ExecuteCommandAsync(cancellationToken);
        if (items.Count > 0)
        {
            await db.Updateable(items.ToList()).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<AiTaskPlanItemDto> AddItemAsync(string planId, AiTaskPlanItemUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(planId, cancellationToken);
        guard.EnsureStructureEditable(plan.Status);
        ValidateSingleItem(request);
        var workspace = new AiWorkspace(plan.TenantId, plan.AppCode, plan.OwnerUserId);
        var item = BuildItemEntities(workspace, plan.ConversationId, plan.Id, [request], new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Single();
        item.Depth = await ResolveDepthAsync(plan.Id, item.ParentItemId, cancellationToken);
        await db.Insertable(item).ExecuteCommandAsync(cancellationToken);
        await BumpRevisionAsync(plan, cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.PlanSaved, item, summary: "任务已新增", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapItem(item);
    }

    private static void ValidateSingleItem(AiTaskPlanItemUpsertRequest request)
    {
        _ = AiTaskPlanValueNormalizer.Required(request.Title, "任务标题不能为空");
        _ = AiTaskPlanValueNormalizer.ItemStatus(request.Status);
        _ = AiTaskPlanValueNormalizer.Priority(request.Priority);
        var ownerType = AiTaskPlanValueNormalizer.OwnerType(request.OwnerType);
        var taskType = AiTaskPlanValueNormalizer.TaskType(request.TaskType);
        _ = AiTaskPlanValidator.ReadStringArray(request.DependsOnJson);
        _ = AiTaskPlanValidator.ReadStringArray(request.AcceptanceCriteriaJson);
        if (ownerType == AiTaskPlanConstants.OwnerType.Tool && string.IsNullOrWhiteSpace(request.ToolCode))
        {
            throw new ValidationException("Tool 任务必须填写 toolCode", ErrorCodes.AiPlanValidationFailed);
        }

        if (taskType == AiTaskPlanConstants.TaskType.Tool && ownerType != AiTaskPlanConstants.OwnerType.Tool)
        {
            throw new ValidationException("Tool 类型任务负责人必须为 Tool", ErrorCodes.AiPlanValidationFailed);
        }
    }

    public async Task<AiTaskPlanItemDto> PatchItemAsync(string itemId, AiTaskPlanItemPatchRequest request, CancellationToken cancellationToken = default)
    {
        var (plan, item) = await RequirePlanAndItemAsync(itemId, cancellationToken);
        guard.EnsureStructureEditable(plan.Status);
        validator.EnsureRevision(request.ExpectedRevision, plan.Revision);
        validator.EnsureUpdatedTime(request.ExpectedUpdatedTime, item.UpdatedTime);
        ApplyPatch(item, request);
        item.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
        await BumpRevisionAsync(plan, cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.PlanSaved, item, summary: "任务已更新", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapItem(item);
    }

    public async Task<AiTaskPlanItemDto> MoveItemAsync(string itemId, AiTaskPlanMoveRequest request, CancellationToken cancellationToken = default)
    {
        var (plan, item) = await RequirePlanAndItemAsync(itemId, cancellationToken);
        guard.EnsureStructureEditable(plan.Status);
        validator.EnsureRevision(request.ExpectedRevision, plan.Revision);
        item.ParentItemId = AiTaskPlanValueNormalizer.Optional(request.ParentItemId);
        item.SortOrder = request.SortOrder;
        item.Depth = await ResolveDepthAsync(plan.Id, item.ParentItemId, cancellationToken);
        item.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
        await BumpRevisionAsync(plan, cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.PlanSaved, item, summary: "任务已移动", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapItem(item);
    }

    public async Task<IReadOnlyList<AiTaskPlanItemDto>> SplitItemAsync(string itemId, AiTaskPlanSplitRequest request, CancellationToken cancellationToken = default)
    {
        var (plan, item) = await RequirePlanAndItemAsync(itemId, cancellationToken);
        guard.EnsureStructureEditable(plan.Status);
        validator.EnsureRevision(request.ExpectedRevision, plan.Revision);
        if (request.Items.Count == 0)
        {
            throw new ValidationException("拆分任务不能为空", ErrorCodes.AiPlanValidationFailed);
        }

        validator.ValidateItems(request.Items);
        var workspace = new AiWorkspace(plan.TenantId, plan.AppCode, plan.OwnerUserId);
        var items = BuildItemEntities(workspace, plan.ConversationId, plan.Id, request.Items, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        foreach (var child in items.Where(child => string.IsNullOrWhiteSpace(child.ParentItemId)))
        {
            child.ParentItemId = item.ParentItemId;
            child.Depth = item.Depth;
        }

        item.IsDeleted = true;
        item.DeletedTime = DateTime.UtcNow;
        await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
        await db.Insertable(items).ExecuteCommandAsync(cancellationToken);
        await BumpRevisionAsync(plan, cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.PlanSaved, item, summary: "任务已拆分", cancellationToken: cancellationToken);
        return items.Select(AiTaskPlanMapper.MapItem).ToList();
    }

    public async Task<AiTaskPlanItemDto> MergeItemsAsync(string targetItemId, AiTaskPlanMergeRequest request, CancellationToken cancellationToken = default)
    {
        var (plan, target) = await RequirePlanAndItemAsync(targetItemId, cancellationToken);
        guard.EnsureStructureEditable(plan.Status);
        validator.EnsureRevision(request.ExpectedRevision, plan.Revision);
        var sourceIds = request.SourceItemIds.Where(id => !string.IsNullOrWhiteSpace(id) && id != target.Id).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var sources = sourceIds.Length == 0
            ? []
            : await db.Queryable<AiTaskPlanItemEntity>().Where(item => !item.IsDeleted && item.PlanId == plan.Id && sourceIds.Contains(item.Id)).ToListAsync(cancellationToken);
        target.Title = AiTaskPlanValueNormalizer.Optional(request.Title) ?? target.Title;
        target.Description = AiTaskPlanValueNormalizer.Optional(request.Description) ?? string.Join("\n\n", new[] { target.Description }.Concat(sources.Select(item => item.Description)).Where(value => !string.IsNullOrWhiteSpace(value)));
        target.UpdatedTime = DateTime.UtcNow;
        foreach (var source in sources)
        {
            source.IsDeleted = true;
            source.DeletedTime = DateTime.UtcNow;
        }

        await db.Updateable(target).ExecuteCommandAsync(cancellationToken);
        if (sources.Count > 0)
        {
            await db.Updateable(sources.ToList()).ExecuteCommandAsync(cancellationToken);
        }

        await BumpRevisionAsync(plan, cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.PlanSaved, target, summary: "任务已合并", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapItem(target);
    }

    public async Task DeleteItemAsync(string itemId, int? expectedRevision, CancellationToken cancellationToken = default)
    {
        var (plan, item) = await RequirePlanAndItemAsync(itemId, cancellationToken);
        guard.EnsureStructureEditable(plan.Status);
        validator.EnsureRevision(expectedRevision, plan.Revision);
        item.IsDeleted = true;
        item.DeletedTime = DateTime.UtcNow;
        await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
        await BumpRevisionAsync(plan, cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.PlanSaved, item, summary: "任务已删除", cancellationToken: cancellationToken);
    }

    public async Task<AiTaskPlanDto> ApproveAsync(string planId, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var plan = await RequirePlanAsync(planId, cancellationToken);
        guard.EnsureCanApprove(plan.Status);
        var items = await LoadItemsAsync(plan.Id, cancellationToken);
        if (items.Count == 0)
        {
            throw new ValidationException("空计划不能批准", ErrorCodes.AiPlanValidationFailed);
        }

        var fromStatus = plan.Status;
        plan.Status = AiTaskPlanConstants.PlanStatus.Approved;
        plan.ApprovedBy = workspace.UserId;
        plan.ApprovedRevision = plan.Revision;
        plan.ApprovedAt = DateTime.UtcNow;
        plan.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(plan).ExecuteCommandAsync(cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.PlanApproved, fromStatus: fromStatus, toStatus: plan.Status, summary: "计划已批准", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapPlan(plan, items);
    }

    public async Task<AiTaskPlanDto> UnapproveAsync(string planId, CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(planId, cancellationToken);
        guard.EnsureCanUnapprove(plan.Status);
        var fromStatus = plan.Status;
        plan.Status = AiTaskPlanConstants.PlanStatus.PlanReady;
        plan.ApprovedBy = null;
        plan.ApprovedRevision = null;
        plan.ApprovedAt = null;
        plan.Revision++;
        plan.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(plan).ExecuteCommandAsync(cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.PlanUnapproved, fromStatus: fromStatus, toStatus: plan.Status, summary: "计划已撤回批准", cancellationToken: cancellationToken);
        return await GetDetailAsync(plan.Id, cancellationToken: cancellationToken);
    }

    public async Task<AiTaskPlanDto> PauseAsync(string planId, CancellationToken cancellationToken = default) =>
        await UpdatePlanStatusAsync(planId, AiTaskPlanConstants.PlanStatus.Paused, guard.EnsureCanPause, AiTaskPlanConstants.Event.AgentPaused, "计划已暂停", cancellationToken);

    public async Task<AiTaskPlanDto> ResumeAsync(string planId, CancellationToken cancellationToken = default) =>
        await UpdatePlanStatusAsync(planId, AiTaskPlanConstants.PlanStatus.Approved, guard.EnsureCanResume, AiTaskPlanConstants.Event.AgentStarted, "计划已恢复，等待继续执行", cancellationToken);

    public async Task<AiTaskPlanDto> CancelAsync(string planId, CancellationToken cancellationToken = default) =>
        await UpdatePlanStatusAsync(planId, AiTaskPlanConstants.PlanStatus.Cancelled, guard.EnsureCanCancel, AiTaskPlanConstants.Event.AgentCancelled, "计划已取消", cancellationToken);

    public async Task<AiTaskPlanItemDto> MarkCompleteAsync(string itemId, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken = default)
    {
        var (plan, item) = await RequirePlanAndItemAsync(itemId, cancellationToken);
        var fromStatus = item.Status;
        item.Status = AiTaskPlanConstants.ItemStatus.Succeeded;
        item.Result = AiTaskPlanValueNormalizer.Optional(request.UserResult) ?? item.Result;
        item.ResultSummary = AiTaskPlanValueNormalizer.Optional(request.UserResult) ?? "用户已确认完成";
        item.CompletedAt = DateTime.UtcNow;
        item.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
        await SaveManualOutputAsync(plan, item, request, cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.TaskCompleted, item, fromStatus: fromStatus, toStatus: item.Status, summary: "任务已由用户标记完成", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapItem(item);
    }

    public async Task<AiTaskPlanItemDto> RetryAsync(string itemId, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken = default)
    {
        var (plan, item) = await RequirePlanAndItemAsync(itemId, cancellationToken);
        guard.EnsureCanRetry(item.Status, item.RetryCount, item.MaxRetryCount);
        var fromStatus = item.Status;
        item.Status = AiTaskPlanConstants.ItemStatus.Pending;
        item.RetryCount++;
        item.BlockedReason = null;
        item.ErrorCode = null;
        item.ErrorMessage = null;
        item.CompletedAt = null;
        item.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.TaskReady, item, fromStatus: fromStatus, toStatus: item.Status, summary: "任务已加入重试队列", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapItem(item);
    }

    public async Task<AiTaskPlanItemDto> SkipAsync(string itemId, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken = default)
    {
        var reason = AiTaskPlanValueNormalizer.Required(request.Reason, "跳过原因不能为空");
        return await UpdateItemTerminalStatusAsync(itemId, AiTaskPlanConstants.ItemStatus.Skipped, AiTaskPlanConstants.Event.TaskSkipped, reason, item => item.SkipReason = reason, cancellationToken);
    }

    public async Task<AiTaskPlanItemDto> BlockAsync(string itemId, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken = default)
    {
        var reason = AiTaskPlanValueNormalizer.Required(request.Reason, "阻塞原因不能为空");
        return await UpdateItemTerminalStatusAsync(itemId, AiTaskPlanConstants.ItemStatus.Blocked, AiTaskPlanConstants.Event.TaskBlocked, reason, item => item.BlockedReason = reason, cancellationToken);
    }

    public async Task<AiTaskPlanItemDto> UnblockAsync(string itemId, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken = default)
    {
        var (plan, item) = await RequirePlanAndItemAsync(itemId, cancellationToken);
        if (item.Status != AiTaskPlanConstants.ItemStatus.Blocked)
        {
            throw new ValidationException("只有阻塞任务可以解除阻塞", ErrorCodes.AiTaskInvalidStatusTransition);
        }

        var fromStatus = item.Status;
        item.Status = AiTaskPlanConstants.ItemStatus.Pending;
        item.BlockedReason = null;
        item.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
        await eventWriter.WriteAsync(plan, AiTaskPlanConstants.Event.TaskReady, item, fromStatus: fromStatus, toStatus: item.Status, summary: "任务已解除阻塞", cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapItem(item);
    }

    private async Task<AiTaskPlanDto> UpdatePlanStatusAsync(string planId, string status, Action<string> guard, string eventName, string summary, CancellationToken cancellationToken)
    {
        var plan = await RequirePlanAsync(planId, cancellationToken);
        guard(plan.Status);
        var fromStatus = plan.Status;
        plan.Status = status;
        plan.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(plan).ExecuteCommandAsync(cancellationToken);
        await eventWriter.WriteAsync(plan, eventName, fromStatus: fromStatus, toStatus: status, summary: summary, cancellationToken: cancellationToken);
        return await GetDetailAsync(plan.Id, cancellationToken: cancellationToken);
    }

    private async Task<AiTaskPlanItemDto> UpdateItemTerminalStatusAsync(
        string itemId,
        string status,
        string eventName,
        string summary,
        Action<AiTaskPlanItemEntity> mutate,
        CancellationToken cancellationToken)
    {
        var (plan, item) = await RequirePlanAndItemAsync(itemId, cancellationToken);
        var fromStatus = item.Status;
        item.Status = status;
        item.CompletedAt = DateTime.UtcNow;
        item.UpdatedTime = DateTime.UtcNow;
        mutate(item);
        await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
        await eventWriter.WriteAsync(plan, eventName, item, fromStatus: fromStatus, toStatus: status, summary: summary, cancellationToken: cancellationToken);
        return AiTaskPlanMapper.MapItem(item);
    }

    private static void ApplyPatch(AiTaskPlanItemEntity item, AiTaskPlanItemPatchRequest request)
    {
        item.Title = AiTaskPlanValueNormalizer.Optional(request.Title) ?? item.Title;
        item.Description = request.Description is null ? item.Description : request.Description.Trim();
        item.Status = request.Status is null ? item.Status : AiTaskPlanValueNormalizer.ItemStatus(request.Status);
        item.Priority = request.Priority is null ? item.Priority : AiTaskPlanValueNormalizer.Priority(request.Priority);
        item.OwnerType = request.OwnerType is null ? item.OwnerType : AiTaskPlanValueNormalizer.OwnerType(request.OwnerType);
        item.TaskType = request.TaskType is null ? item.TaskType : AiTaskPlanValueNormalizer.TaskType(request.TaskType);
        item.SortOrder = request.SortOrder ?? item.SortOrder;
        item.ParentItemId = request.ParentItemId is null ? item.ParentItemId : AiTaskPlanValueNormalizer.Optional(request.ParentItemId);
        item.DependsOnJson = request.DependsOnJson is null ? item.DependsOnJson : AiTaskPlanValueNormalizer.Optional(request.DependsOnJson);
        item.AcceptanceCriteriaJson = request.AcceptanceCriteriaJson is null ? item.AcceptanceCriteriaJson : AiTaskPlanValueNormalizer.Optional(request.AcceptanceCriteriaJson);
        item.ToolCode = request.ToolCode is null ? item.ToolCode : AiTaskPlanValueNormalizer.Optional(request.ToolCode);
        item.ExecutionHint = request.ExecutionHint is null ? item.ExecutionHint : AiTaskPlanValueNormalizer.Optional(request.ExecutionHint);
        item.Result = request.Result is null ? item.Result : AiTaskPlanValueNormalizer.Optional(request.Result);
        item.ResultSummary = request.ResultSummary is null ? item.ResultSummary : AiTaskPlanValueNormalizer.Optional(request.ResultSummary);
        item.EvidenceJson = request.EvidenceJson is null ? item.EvidenceJson : AiTaskPlanValueNormalizer.Optional(request.EvidenceJson);
        item.ErrorCode = request.ErrorCode is null ? item.ErrorCode : AiTaskPlanValueNormalizer.Optional(request.ErrorCode);
        item.ErrorMessage = request.ErrorMessage is null ? item.ErrorMessage : AiTaskPlanValueNormalizer.Optional(request.ErrorMessage);
        item.BlockedReason = request.BlockedReason is null ? item.BlockedReason : AiTaskPlanValueNormalizer.Optional(request.BlockedReason);
        item.SkipReason = request.SkipReason is null ? item.SkipReason : AiTaskPlanValueNormalizer.Optional(request.SkipReason);
    }

    private async Task SaveManualOutputAsync(AiTaskPlanEntity plan, AiTaskPlanItemEntity item, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken)
    {
        var content = AiTaskPlanValueNormalizer.Optional(request.UserResult);
        if (content is null)
        {
            return;
        }

        var output = new AiTaskPlanItemOutputEntity
        {
            TenantId = plan.TenantId,
            AppCode = plan.AppCode,
            OwnerUserId = plan.OwnerUserId,
            ConversationId = plan.ConversationId,
            PlanId = plan.Id,
            ItemId = item.Id,
            RunId = plan.RunId,
            OutputType = "User",
            ResultSummary = content.Length > 180 ? $"{content[..180]}..." : content,
            Content = content
        };
        await db.Insertable(output).ExecuteCommandAsync(cancellationToken);
    }

    private static List<AiTaskPlanItemEntity> BuildItemEntities(
        AiWorkspace workspace,
        string conversationId,
        string planId,
        IReadOnlyList<AiTaskPlanItemUpsertRequest> requests,
        ISet<string> existingIds)
    {
        var entities = new List<AiTaskPlanItemEntity>();
        var idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var order = 1;
        foreach (var request in requests)
        {
            var entityId = !string.IsNullOrWhiteSpace(request.Id) && existingIds.Contains(request.Id.Trim())
                ? request.Id.Trim()
                : Guid.NewGuid().ToString("N");
            if (!string.IsNullOrWhiteSpace(request.Id))
            {
                idMap[request.Id.Trim()] = entityId;
            }

            entities.Add(new AiTaskPlanItemEntity
            {
                Id = entityId,
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                OwnerUserId = workspace.UserId,
                ConversationId = conversationId,
                PlanId = planId,
                ParentItemId = request.ParentItemId,
                Title = AiTaskPlanValueNormalizer.Required(request.Title, "任务标题不能为空"),
                Description = request.Description.Trim(),
                Status = AiTaskPlanValueNormalizer.ItemStatus(request.Status),
                Priority = AiTaskPlanValueNormalizer.Priority(request.Priority),
                OwnerType = AiTaskPlanValueNormalizer.OwnerType(request.OwnerType),
                TaskType = AiTaskPlanValueNormalizer.TaskType(request.TaskType),
                SortOrder = request.SortOrder > 0 ? request.SortOrder : order,
                DependsOnJson = request.DependsOnJson,
                AcceptanceCriteriaJson = AiTaskPlanValueNormalizer.Optional(request.AcceptanceCriteriaJson),
                ToolCode = AiTaskPlanValueNormalizer.Optional(request.ToolCode),
                ExecutionHint = AiTaskPlanValueNormalizer.Optional(request.ExecutionHint),
                MaxRetryCount = request.MaxRetryCount
            });
            order++;
        }

        foreach (var entity in entities)
        {
            if (!string.IsNullOrWhiteSpace(entity.ParentItemId) && idMap.TryGetValue(entity.ParentItemId, out var parentId))
            {
                entity.ParentItemId = parentId;
            }

            entity.DependsOnJson = RewriteDependencyIds(entity.DependsOnJson, idMap);
        }

        ApplyDepths(entities);
        return entities;
    }

    private static string? RewriteDependencyIds(string? dependsOnJson, IReadOnlyDictionary<string, string> idMap)
    {
        var dependencies = AiTaskPlanValidator.ReadStringArray(dependsOnJson);
        if (dependencies.Count == 0)
        {
            return null;
        }

        var rewritten = dependencies.Select(id => idMap.TryGetValue(id, out var mapped) ? mapped : id).ToList();
        return JsonSerializer.Serialize(rewritten, JsonOptions);
    }

    private static void ApplyDepths(IReadOnlyList<AiTaskPlanItemEntity> entities)
    {
        var byId = entities.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var item in entities)
        {
            var depth = 0;
            var cursor = item.ParentItemId;
            while (!string.IsNullOrWhiteSpace(cursor) && byId.TryGetValue(cursor, out var parent))
            {
                depth++;
                cursor = parent.ParentItemId;
            }

            item.Depth = depth;
        }
    }

    private static AiTaskPlanItemEntity CloneItem(AiTaskPlanItemEntity source, string planId, IReadOnlyDictionary<string, string> idMap)
    {
        var cloneId = idMap[source.Id];
        return new AiTaskPlanItemEntity
        {
            Id = cloneId,
            TenantId = source.TenantId,
            AppCode = source.AppCode,
            OwnerUserId = source.OwnerUserId,
            ConversationId = source.ConversationId,
            PlanId = planId,
            ParentItemId = source.ParentItemId is not null && idMap.TryGetValue(source.ParentItemId, out var parentId) ? parentId : null,
            Title = source.Title,
            Description = source.Description,
            Status = AiTaskPlanConstants.ItemStatus.Pending,
            Priority = source.Priority,
            OwnerType = source.OwnerType,
            TaskType = source.TaskType,
            SortOrder = source.SortOrder,
            Depth = source.Depth,
            DependsOnJson = RewriteDependencyIds(source.DependsOnJson, idMap),
            AcceptanceCriteriaJson = source.AcceptanceCriteriaJson,
            ToolCode = source.ToolCode,
            ExecutionHint = source.ExecutionHint,
            MaxRetryCount = source.MaxRetryCount
        };
    }

    private async Task<int> ResolveDepthAsync(string planId, string? parentItemId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentItemId))
        {
            return 0;
        }

        var parent = await db.Queryable<AiTaskPlanItemEntity>()
            .FirstAsync(item => !item.IsDeleted && item.PlanId == planId && item.Id == parentItemId, cancellationToken)
            ?? throw new NotFoundException("父任务不存在", ErrorCodes.AiTaskNotFound);
        return parent.Depth + 1;
    }

    private async Task BumpRevisionAsync(AiTaskPlanEntity plan, CancellationToken cancellationToken)
    {
        plan.Revision++;
        plan.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(plan).ExecuteCommandAsync(cancellationToken);
    }

    private async Task SoftDeleteItemsAsync(IReadOnlyList<AiTaskPlanItemEntity> items, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            item.IsDeleted = true;
            item.DeletedTime = DateTime.UtcNow;
        }

        if (items.Count > 0)
        {
            await db.Updateable(items.ToList()).ExecuteCommandAsync(cancellationToken);
        }
    }

    private async Task<AiTaskPlanEntity> RequirePlanAsync(string planId, CancellationToken cancellationToken) =>
        await db.Queryable<AiTaskPlanEntity>().FirstAsync(item => item.Id == planId && !item.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("任务计划不存在", ErrorCodes.AiTaskPlanNotFound);

    private async Task<(AiTaskPlanEntity Plan, AiTaskPlanItemEntity Item)> RequirePlanAndItemAsync(string itemId, CancellationToken cancellationToken)
    {
        var item = await db.Queryable<AiTaskPlanItemEntity>().FirstAsync(row => row.Id == itemId && !row.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("任务不存在", ErrorCodes.AiTaskNotFound);
        var plan = await RequirePlanAsync(item.PlanId, cancellationToken);
        return (plan, item);
    }

    private async Task<IReadOnlyList<AiTaskPlanItemEntity>> LoadItemsAsync(string planId, CancellationToken cancellationToken) =>
        await db.Queryable<AiTaskPlanItemEntity>()
            .Where(item => !item.IsDeleted && item.PlanId == planId)
            .OrderBy(item => item.Depth)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
}

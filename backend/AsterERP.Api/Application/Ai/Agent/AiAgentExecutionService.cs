using AsterERP.Api.Modules.Ai;
using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Agent;

public sealed class AiAgentExecutionService(
    ISqlSugarClient db,
    AiTaskPlanGuard guard,
    AiTaskPlanEventWriter eventWriter,
    AiKernelFunctionService toolExecutor) : IAiAgentExecutionService
{
    public async Task<AiAgentExecutionResult> ExecuteAsync(
        string planId,
        string runId,
        string? modelConfigId = null,
        string? userInstruction = null,
        IReadOnlyList<string>? enabledToolCodes = null,
        IReadOnlyList<string>? enabledToolDomains = null,
        Func<AiTaskPlanEventDto, CancellationToken, Task>? onEvent = null,
        CancellationToken cancellationToken = default)
    {
        var plan = await RequirePlanAsync(planId, cancellationToken);
        guard.EnsureCanExecute(plan.Status);
        var fromStatus = plan.Status;
        var events = new List<AiTaskPlanEventDto>();
        var items = await LoadItemsAsync(plan.Id, cancellationToken);
        var executableItems = items
            .Where(item => item.Status is AiTaskPlanConstants.ItemStatus.Pending or AiTaskPlanConstants.ItemStatus.Ready or AiTaskPlanConstants.ItemStatus.Failed)
            .ToList();

        if (executableItems.Count == 0)
        {
            return new AiAgentExecutionResult
            {
                PlanId = plan.Id,
                RunId = runId,
                PlanStatus = plan.Status,
                Summary = "当前计划没有可执行任务。",
                Events = events
            };
        }

        plan.Status = AiTaskPlanConstants.PlanStatus.Running;
        plan.RunId = runId;
        plan.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(plan).ExecuteCommandAsync(cancellationToken);

        await EmitAsync(
            plan,
            AiTaskPlanConstants.Event.AgentStarted,
            null,
            runId,
            fromStatus,
            plan.Status,
            "Agent 已接收已批准计划，开始按顺序执行可自动处理的任务。",
            new
            {
                modelConfigId,
                userInstruction,
                enabledToolCodes,
                enabledToolDomains,
            },
            onEvent,
            events,
            cancellationToken);

        var toolSelection = AiKernelFunctionSelection.From(enabledToolCodes, enabledToolDomains);
        foreach (var item in executableItems)
        {
            if (IsUserActionTask(item))
            {
                await MoveItemToWaitingAsync(plan, item, runId, onEvent, events, cancellationToken);
                continue;
            }

            await ExecuteToolItemAsync(plan, item, runId, modelConfigId, userInstruction, toolSelection, onEvent, events, cancellationToken);
        }

        var latestItems = await LoadItemsAsync(plan.Id, cancellationToken);
        plan.Status = ResolvePlanStatus(latestItems);
        plan.UpdatedTime = DateTime.UtcNow;
        if (plan.Status == AiTaskPlanConstants.PlanStatus.Completed)
        {
            plan.CompletedAt = DateTime.UtcNow;
        }

        await db.Updateable(plan).ExecuteCommandAsync(cancellationToken);
        var summary = BuildExecutionSummary(latestItems);
        await EmitAsync(
            plan,
            plan.Status == AiTaskPlanConstants.PlanStatus.Completed ? AiTaskPlanConstants.Event.AgentCompleted : AiTaskPlanConstants.Event.ExecutionQueueBuilt,
            null,
            runId,
            AiTaskPlanConstants.PlanStatus.Running,
            plan.Status,
            summary,
            new
            {
                total = latestItems.Count,
                succeeded = latestItems.Count(item => item.Status == AiTaskPlanConstants.ItemStatus.Succeeded),
                failed = latestItems.Count(item => item.Status == AiTaskPlanConstants.ItemStatus.Failed),
                waitingUser = latestItems.Count(item => item.Status == AiTaskPlanConstants.ItemStatus.WaitingUser),
                blocked = latestItems.Count(item => item.Status == AiTaskPlanConstants.ItemStatus.Blocked)
            },
            onEvent,
            events,
            cancellationToken);

        return new AiAgentExecutionResult
        {
            PlanId = plan.Id,
            RunId = runId,
            PlanStatus = plan.Status,
            Summary = summary,
            Events = events
        };
    }

    private async Task ExecuteToolItemAsync(
        AiTaskPlanEntity plan,
        AiTaskPlanItemEntity item,
        string runId,
        string? modelConfigId,
        string? userInstruction,
        AiKernelFunctionSelection toolSelection,
        Func<AiTaskPlanEventDto, CancellationToken, Task>? onEvent,
        List<AiTaskPlanEventDto> events,
        CancellationToken cancellationToken)
    {
        var fromStatus = item.Status;
        var now = DateTime.UtcNow;
        var definition = toolExecutor.GetDefinition(item.ToolCode ?? string.Empty);
        if (definition.RequiresConfirmation)
        {
            item.Status = AiTaskPlanConstants.ItemStatus.WaitingUser;
            item.StartedAt ??= now;
            item.ResultSummary = "高风险工具需要人工确认后执行。";
            item.BlockedReason = null;
            item.ErrorCode = null;
            item.ErrorMessage = null;
            item.UpdatedTime = now;
            await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
            await EmitAsync(plan, AiTaskPlanConstants.Event.TaskWaitingUser, item, runId, fromStatus, item.Status, item.ResultSummary, new { item.Id, item.ToolCode, definition.RiskLevel }, onEvent, events, cancellationToken);
            return;
        }

        item.Status = AiTaskPlanConstants.ItemStatus.InProgress;
        item.StartedAt ??= now;
        item.ErrorCode = null;
        item.ErrorMessage = null;
        item.BlockedReason = null;
        item.UpdatedTime = now;
        await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
        await EmitAsync(plan, AiTaskPlanConstants.Event.TaskToolCallStarted, item, runId, fromStatus, item.Status, "开始执行工具任务。", new { item.Id, item.ToolCode }, onEvent, events, cancellationToken);

        try
        {
            var output = await toolExecutor.ExecutePlanItemAsync(plan, item, runId, modelConfigId, userInstruction, toolSelection, cancellationToken);
            item.Status = AiTaskPlanConstants.ItemStatus.Succeeded;
            item.CompletedAt = DateTime.UtcNow;
            item.ResultSummary = output.ResultSummary;
            item.Result = output.Content;
            item.EvidenceJson = output.EvidenceJson;
            item.UpdatedTime = item.CompletedAt;
            await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
            await SaveToolOutputAsync(plan, item, runId, output, null, cancellationToken);
            await EmitAsync(plan, AiTaskPlanConstants.Event.TaskToolCallCompleted, item, runId, AiTaskPlanConstants.ItemStatus.InProgress, item.Status, output.ResultSummary, new { item.Id, item.ToolCode, output.OutputType }, onEvent, events, cancellationToken);
        }
        catch (Exception ex)
        {
            item.Status = AiTaskPlanConstants.ItemStatus.Failed;
            item.ErrorCode = ResolveErrorCode(ex).ToString();
            item.ErrorMessage = ex.Message;
            item.CompletedAt = DateTime.UtcNow;
            item.UpdatedTime = item.CompletedAt;
            await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
            await SaveToolOutputAsync(plan, item, runId, null, ex, cancellationToken);
            await EmitAsync(plan, AiTaskPlanConstants.Event.TaskFailed, item, runId, AiTaskPlanConstants.ItemStatus.InProgress, item.Status, ex.Message, new { item.Id, item.ToolCode, errorCode = item.ErrorCode }, onEvent, events, cancellationToken);
        }
    }

    private async Task MoveItemToWaitingAsync(
        AiTaskPlanEntity plan,
        AiTaskPlanItemEntity item,
        string runId,
        Func<AiTaskPlanEventDto, CancellationToken, Task>? onEvent,
        List<AiTaskPlanEventDto> events,
        CancellationToken cancellationToken)
    {
        var fromStatus = item.Status;
        item.Status = AiTaskPlanConstants.ItemStatus.WaitingUser;
        item.StartedAt ??= DateTime.UtcNow;
        item.ResultSummary = "等待用户完成人工任务。";
        item.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
        await EmitAsync(plan, AiTaskPlanConstants.Event.TaskWaitingUser, item, runId, fromStatus, item.Status, item.ResultSummary, new { item.Id, item.Title, item.OwnerType, item.TaskType }, onEvent, events, cancellationToken);
    }

    private async Task SaveToolOutputAsync(
        AiTaskPlanEntity plan,
        AiTaskPlanItemEntity item,
        string runId,
        AiTaskAgentRunOutput? output,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        var entity = new AiTaskPlanItemOutputEntity
        {
            TenantId = plan.TenantId,
            AppCode = plan.AppCode,
            OwnerUserId = plan.OwnerUserId,
            ConversationId = plan.ConversationId,
            PlanId = plan.Id,
            ItemId = item.Id,
            RunId = runId,
            OutputType = output?.OutputType ?? "Error",
            ResultSummary = output?.ResultSummary ?? exception?.Message ?? "工具执行失败",
            Content = output?.Content,
            EvidenceJson = output?.EvidenceJson,
            ErrorCode = exception is null ? null : ResolveErrorCode(exception).ToString(),
            ErrorMessage = exception?.Message
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private static string ResolvePlanStatus(IReadOnlyList<AiTaskPlanItemEntity> items)
    {
        if (items.Any(item => item.Status == AiTaskPlanConstants.ItemStatus.Failed))
        {
            return AiTaskPlanConstants.PlanStatus.Failed;
        }

        if (items.Any(item => item.Status == AiTaskPlanConstants.ItemStatus.Blocked))
        {
            return AiTaskPlanConstants.PlanStatus.Blocked;
        }

        if (items.Any(item => item.Status == AiTaskPlanConstants.ItemStatus.WaitingUser))
        {
            return AiTaskPlanConstants.PlanStatus.PartialCompleted;
        }

        return items.All(item => item.Status is AiTaskPlanConstants.ItemStatus.Succeeded or AiTaskPlanConstants.ItemStatus.Skipped)
            ? AiTaskPlanConstants.PlanStatus.Completed
            : AiTaskPlanConstants.PlanStatus.PartialCompleted;
    }

    private static string BuildExecutionSummary(IReadOnlyList<AiTaskPlanItemEntity> items) =>
        $"计划执行完成：成功 {items.Count(item => item.Status == AiTaskPlanConstants.ItemStatus.Succeeded)}，失败 {items.Count(item => item.Status == AiTaskPlanConstants.ItemStatus.Failed)}，等待用户 {items.Count(item => item.Status == AiTaskPlanConstants.ItemStatus.WaitingUser)}。";

    private static int ResolveErrorCode(Exception ex) =>
        ex is BusinessException businessException ? businessException.Code : ErrorCodes.AiTaskExecutionFailed;

    private async Task<AiAgentExecutionResult> MoveUserActionTasksToWaitingAsync(
        AiTaskPlanEntity plan,
        IReadOnlyList<AiTaskPlanItemEntity> executableItems,
        string runId,
        string fromStatus,
        string? modelConfigId,
        string? userInstruction,
        IReadOnlyList<string>? enabledToolCodes,
        IReadOnlyList<string>? enabledToolDomains,
        Func<AiTaskPlanEventDto, CancellationToken, Task>? onEvent,
        List<AiTaskPlanEventDto> events,
        CancellationToken cancellationToken)
    {
        var previousItemStatuses = executableItems.ToDictionary(item => item.Id, item => item.Status);
        var now = DateTime.UtcNow;
        db.Ado.BeginTran();
        try
        {
            plan.Status = AiTaskPlanConstants.PlanStatus.PartialCompleted;
            plan.RunId = runId;
            plan.UpdatedTime = now;
            await db.Updateable(plan).ExecuteCommandAsync(cancellationToken);

            foreach (var item in executableItems)
            {
                item.Status = AiTaskPlanConstants.ItemStatus.WaitingUser;
                item.StartedAt ??= now;
                item.CompletedAt = null;
                item.BlockedReason = null;
                item.ErrorCode = null;
                item.ErrorMessage = null;
                item.ResultSummary = "等待用户完成人工任务。";
                item.UpdatedTime = now;
                await db.Updateable(item).ExecuteCommandAsync(cancellationToken);
            }

            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }

        var summary = $"已将 {executableItems.Count} 个人工任务推进至 WaitingUser，等待用户人工处理；未执行工具或业务写入。";
        await EmitAsync(
            plan,
            AiTaskPlanConstants.Event.AgentStarted,
            null,
            runId,
            fromStatus,
            AiTaskPlanConstants.PlanStatus.Running,
            "Agent 已接收已批准人工计划。",
            new
            {
                modelConfigId,
                userInstruction,
                enabledToolCodes,
                enabledToolDomains
            },
            onEvent,
            events,
            cancellationToken);

        await EmitAsync(
            plan,
            AiTaskPlanConstants.Event.ExecutionQueueBuilt,
            null,
            runId,
            AiTaskPlanConstants.PlanStatus.Running,
            plan.Status,
            summary,
            new
            {
                executionKind = "UserAction",
                waitingItemIds = executableItems.Select(item => item.Id)
            },
            onEvent,
            events,
            cancellationToken);

        foreach (var item in executableItems)
        {
            await EmitAsync(
                plan,
                AiTaskPlanConstants.Event.TaskWaitingUser,
                item,
                runId,
                previousItemStatuses[item.Id],
                item.Status,
                "等待用户完成人工任务。",
                new { item.Id, item.Title, item.OwnerType, item.TaskType },
                onEvent,
                events,
                cancellationToken);
        }

        return new AiAgentExecutionResult
        {
            PlanId = plan.Id,
            RunId = runId,
            PlanStatus = plan.Status,
            Summary = summary,
            Events = events
        };
    }

    private static bool IsUserActionTask(AiTaskPlanItemEntity item) =>
        string.Equals(item.OwnerType, AiTaskPlanConstants.OwnerType.User, StringComparison.OrdinalIgnoreCase) &&
        string.IsNullOrWhiteSpace(item.ToolCode);

    private async Task<AiTaskPlanEventDto> EmitAsync(
        AiTaskPlanEntity plan,
        string eventName,
        AiTaskPlanItemEntity? item,
        string runId,
        string? fromStatus,
        string? toStatus,
        string? summary,
        object? payload,
        Func<AiTaskPlanEventDto, CancellationToken, Task>? onEvent,
        List<AiTaskPlanEventDto> events,
        CancellationToken cancellationToken)
    {
        var eventDto = await eventWriter.WriteAsync(plan, eventName, item, runId, fromStatus, toStatus, summary, payload, null, cancellationToken);
        events.Add(eventDto);
        if (onEvent is not null)
        {
            await onEvent(eventDto, cancellationToken);
        }

        return eventDto;
    }

    private async Task<AiTaskPlanEntity> RequirePlanAsync(string planId, CancellationToken cancellationToken) =>
        await db.Queryable<AiTaskPlanEntity>().FirstAsync(item => item.Id == planId && !item.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("任务计划不存在", ErrorCodes.AiTaskPlanNotFound);

    private async Task<IReadOnlyList<AiTaskPlanItemEntity>> LoadItemsAsync(string planId, CancellationToken cancellationToken) =>
        await db.Queryable<AiTaskPlanItemEntity>()
            .Where(item => !item.IsDeleted && item.PlanId == planId)
            .OrderBy(item => item.Depth)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
}

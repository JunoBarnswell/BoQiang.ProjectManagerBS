using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiObservabilityService(ISqlSugarClient db)
{
    public async Task<AiObservabilitySummaryDto> GetSummaryAsync(AiUsageQuery query, CancellationToken cancellationToken = default)
    {
        var usageQuery = ApplyUsageFilters(db.Queryable<AiUsageLogEntity>().Where(item => !item.IsDeleted), query);
        return new AiObservabilitySummaryDto
        {
            RequestCount = await usageQuery.CountAsync(cancellationToken),
            SuccessCount = await usageQuery.Where(item => item.IsSuccess).CountAsync(cancellationToken),
            FailedCount = await usageQuery.Where(item => !item.IsSuccess).CountAsync(cancellationToken),
            PromptTokens = await usageQuery.SumAsync(item => item.PromptTokens),
            CompletionTokens = await usageQuery.SumAsync(item => item.CompletionTokens),
            ReasoningTokens = await usageQuery.SumAsync(item => item.ReasoningTokens),
            TotalTokens = await usageQuery.SumAsync(item => item.TotalTokens),
            CostAmount = await usageQuery.SumAsync(item => item.CostAmount),
            RunCount = await ApplyRunFilters(db.Queryable<AiChatRunEntity>().Where(item => !item.IsDeleted), query).CountAsync(cancellationToken),
            RunningRunCount = await db.Queryable<AiChatRunEntity>()
                .Where(item => !item.IsDeleted && (item.Status == "Queued" || item.Status == "Running"))
                .CountAsync(cancellationToken),
            ToolExecutionCount = await db.Queryable<AiToolExecutionLogEntity>().Where(item => !item.IsDeleted).CountAsync(cancellationToken),
            FailedToolExecutionCount = await db.Queryable<AiToolExecutionLogEntity>().Where(item => !item.IsDeleted && item.Status == "Failed").CountAsync(cancellationToken)
        };
    }

    public async Task<IReadOnlyList<AiObservabilityTrendPointDto>> GetTrendsAsync(AiUsageQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = EnsureBoundedTrendRange(query);
        var rows = await ApplyUsageFilters(db.Queryable<AiUsageLogEntity>().Where(item => !item.IsDeleted), normalized)
            .GroupBy(item => item.RequestStartedAt.Date)
            .Select(item => new AiObservabilityTrendPointDto
            {
                Bucket = item.RequestStartedAt.Date.ToString(),
                RequestCount = SqlFunc.AggregateCount(item.Id),
                SuccessCount = SqlFunc.AggregateSum(item.IsSuccess ? 1 : 0),
                FailedCount = SqlFunc.AggregateSum(item.IsSuccess ? 0 : 1),
                TotalTokens = SqlFunc.AggregateSum(item.TotalTokens),
                CostAmount = SqlFunc.AggregateSum(item.CostAmount)
            })
            .ToListAsync(cancellationToken);

        return rows.OrderBy(item => item.Bucket, StringComparer.Ordinal).ToList();
    }

    public async Task<GridPageResult<AiRunListItemDto>> GetRunsAsync(GridQuery gridQuery, AiObservabilityRunQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = ApplyRunFilters(db.Queryable<AiChatRunEntity>().Where(item => !item.IsDeleted), query);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Mode))
        {
            var mode = query.Mode.Trim();
            dbQuery = dbQuery.Where(item => item.Mode == mode);
        }

        if (!string.IsNullOrWhiteSpace(gridQuery.Keyword))
        {
            var keyword = gridQuery.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.Id.Contains(keyword) || item.ConversationId.Contains(keyword) || (item.ErrorMessage != null && item.ErrorMessage.Contains(keyword)));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(gridQuery.PageIndex, 1), Math.Clamp(gridQuery.PageSize, 1, 200), total);
        return new GridPageResult<AiRunListItemDto> { Total = total.Value, Items = rows.Select(MapRun).ToList() };
    }

    public async Task<AiRunDetailDto> GetRunDetailAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await db.Queryable<AiChatRunEntity>()
            .FirstAsync(item => item.Id == runId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("AI Run 不存在", ErrorCodes.AiRunNotFound);
        var messages = await db.Queryable<AiMessageEntity>()
            .Where(item => !item.IsDeleted && item.RunId == runId)
            .OrderBy(item => item.Seq)
            .ToListAsync(cancellationToken);
        var plans = await db.Queryable<AiTaskPlanEntity>()
            .Where(item => !item.IsDeleted && item.RunId == runId)
            .OrderBy(item => item.CreatedTime)
            .ToListAsync(cancellationToken);
        var planIds = plans.Select(item => item.Id).ToList();
        var planItems = planIds.Count == 0
            ? []
            : await db.Queryable<AiTaskPlanItemEntity>()
                .Where(item => !item.IsDeleted && planIds.Contains(item.PlanId))
                .OrderBy(item => item.SortOrder)
                .ToListAsync(cancellationToken);
        var toolExecutions = await db.Queryable<AiToolExecutionLogEntity>()
            .Where(item => !item.IsDeleted && item.RunId == runId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);

        var dto = new AiRunDetailDto
        {
            Id = run.Id,
            ConversationId = run.ConversationId,
            Mode = run.Mode,
            Status = run.Status,
            TotalTokens = run.TotalTokens,
            ErrorCode = run.ErrorCode,
            ErrorMessage = run.ErrorMessage,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Messages = messages.Select(MapMessage).ToList(),
            TaskPlans = plans.Select(plan => MapTaskPlan(plan, planItems.Where(item => item.PlanId == plan.Id).ToList())).ToList(),
            ToolExecutions = toolExecutions.Select(AiKernelFunctionMapper.MapInvocation).ToList()
        };
        return dto;
    }

    public async Task<GridPageResult<AiToolInvocationDto>> GetToolExecutionsAsync(AiToolExecutionQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = db.Queryable<AiToolExecutionLogEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.RunId))
        {
            var runId = query.RunId.Trim();
            dbQuery = dbQuery.Where(item => item.RunId == runId);
        }

        if (!string.IsNullOrWhiteSpace(query.ToolCode))
        {
            var toolCode = query.ToolCode.Trim();
            dbQuery = dbQuery.Where(item => item.ToolCode == toolCode);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (query.StartedAt is not null)
        {
            dbQuery = dbQuery.Where(item => item.CreatedTime >= query.StartedAt.Value);
        }

        if (query.EndedAt is not null)
        {
            dbQuery = dbQuery.Where(item => item.CreatedTime <= query.EndedAt.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.ToolName.Contains(keyword) || (item.ToolCode != null && item.ToolCode.Contains(keyword)) || (item.ErrorMessage != null && item.ErrorMessage.Contains(keyword)));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total);
        return new GridPageResult<AiToolInvocationDto> { Total = total.Value, Items = rows.Select(AiKernelFunctionMapper.MapInvocation).ToList() };
    }

    public async Task<IReadOnlyList<AiFailureSummaryDto>> GetFailuresAsync(AiUsageQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await ApplyRunFilters(db.Queryable<AiChatRunEntity>().Where(item => !item.IsDeleted && item.Status == "Failed"), query)
            .GroupBy(item => new { item.ErrorCode, item.ErrorMessage })
            .Select(item => new AiFailureSummaryDto
            {
                ErrorCode = item.ErrorCode ?? "Unknown",
                ErrorMessage = item.ErrorMessage ?? "未知错误",
                Count = SqlFunc.AggregateCount(item.Id)
            })
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(item => item.Count).Take(20).ToList();
    }

    private static AiUsageQuery EnsureBoundedTrendRange(AiUsageQuery query)
    {
        var endedAt = query.EndedAt ?? DateTime.UtcNow;
        var startedAt = query.StartedAt ?? endedAt.AddDays(-30);
        if (endedAt < startedAt)
        {
            throw new ValidationException("趋势结束时间不能早于开始时间", ErrorCodes.ParameterInvalid);
        }

        if ((endedAt - startedAt).TotalDays > 90)
        {
            throw new ValidationException("趋势查询最长支持 90 天", ErrorCodes.ParameterInvalid);
        }

        return new AiUsageQuery
        {
            StartedAt = startedAt,
            EndedAt = endedAt,
            UserId = query.UserId,
            ProviderCode = query.ProviderCode,
            ModelCode = query.ModelCode
        };
    }

    private static ISugarQueryable<AiUsageLogEntity> ApplyUsageFilters(ISugarQueryable<AiUsageLogEntity> query, AiUsageQuery filters)
    {
        if (filters.StartedAt is not null)
        {
            query = query.Where(item => item.RequestStartedAt >= filters.StartedAt.Value);
        }

        if (filters.EndedAt is not null)
        {
            query = query.Where(item => item.RequestStartedAt <= filters.EndedAt.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.UserId))
        {
            var userId = filters.UserId.Trim();
            query = query.Where(item => item.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(filters.ProviderCode))
        {
            var providerCode = filters.ProviderCode.Trim();
            query = query.Where(item => item.ProviderCode == providerCode);
        }

        if (!string.IsNullOrWhiteSpace(filters.ModelCode))
        {
            var modelCode = filters.ModelCode.Trim();
            query = query.Where(item => item.ModelCode == modelCode);
        }

        return query;
    }

    private static ISugarQueryable<AiChatRunEntity> ApplyRunFilters(ISugarQueryable<AiChatRunEntity> query, AiUsageQuery filters)
    {
        if (filters.StartedAt is not null)
        {
            query = query.Where(item => item.CreatedTime >= filters.StartedAt.Value);
        }

        if (filters.EndedAt is not null)
        {
            query = query.Where(item => item.CreatedTime <= filters.EndedAt.Value);
        }

        return query;
    }

    private static AiRunListItemDto MapRun(AiChatRunEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        Mode = entity.Mode,
        Status = entity.Status,
        TotalTokens = entity.TotalTokens,
        ErrorCode = entity.ErrorCode,
        ErrorMessage = entity.ErrorMessage,
        StartedAt = entity.StartedAt,
        CompletedAt = entity.CompletedAt
    };

    private static AiMessageDto MapMessage(AiMessageEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        RunId = entity.RunId,
        AgentProfileId = entity.AgentProfileId,
        Role = entity.Role,
        Seq = entity.Seq,
        Content = entity.Content,
        ReasoningContent = entity.ReasoningContent,
        Status = entity.Status,
        FinishReason = entity.FinishReason,
        TokenCount = entity.TokenCount,
        CreatedTime = entity.CreatedTime
    };

    private static AiTaskPlanDto MapTaskPlan(AiTaskPlanEntity entity, IReadOnlyList<AiTaskPlanItemEntity> items) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        RunId = entity.RunId,
        Title = entity.Title,
        Goal = entity.Goal,
        Status = entity.Status,
        Mode = entity.Mode,
        VersionNo = entity.VersionNo,
        Revision = entity.Revision,
        ExecutionStrategy = entity.ExecutionStrategy,
        RisksJson = entity.RisksJson,
        AssumptionsJson = entity.AssumptionsJson,
        MetadataJson = entity.MetadataJson,
        ApprovedBy = entity.ApprovedBy,
        ApprovedRevision = entity.ApprovedRevision,
        ApprovedAt = entity.ApprovedAt,
        CompletedAt = entity.CompletedAt,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime,
        Items = items.Select(MapTaskItem).ToList(),
        Events = [],
        Progress = new AiTaskPlanProgressDto
        {
            TotalCount = items.Count,
            CompletedCount = items.Count(item => item.Status == "Succeeded"),
            FailedCount = items.Count(item => item.Status == "Failed"),
            BlockedCount = items.Count(item => item.Status == "Blocked"),
            WaitingUserCount = items.Count(item => item.Status == "WaitingUser"),
            Percent = items.Count == 0 ? 0 : (int)Math.Round(items.Count(item => item.Status is "Succeeded" or "Skipped") * 100m / items.Count, 0)
        }
    };

    private static AiTaskPlanItemDto MapTaskItem(AiTaskPlanItemEntity entity) => new()
    {
        Id = entity.Id,
        PlanId = entity.PlanId,
        ParentItemId = entity.ParentItemId,
        Title = entity.Title,
        Description = entity.Description,
        Status = entity.Status,
        Priority = entity.Priority,
        OwnerType = entity.OwnerType,
        TaskType = entity.TaskType,
        SortOrder = entity.SortOrder,
        Depth = entity.Depth,
        DependsOnJson = entity.DependsOnJson,
        AcceptanceCriteriaJson = entity.AcceptanceCriteriaJson,
        ToolCode = entity.ToolCode,
        ExecutionHint = entity.ExecutionHint,
        Result = entity.Result,
        ResultSummary = entity.ResultSummary,
        EvidenceJson = entity.EvidenceJson,
        ErrorCode = entity.ErrorCode,
        ErrorMessage = entity.ErrorMessage,
        BlockedReason = entity.BlockedReason,
        SkipReason = entity.SkipReason,
        RetryCount = entity.RetryCount,
        MaxRetryCount = entity.MaxRetryCount,
        StartedAt = entity.StartedAt,
        CompletedAt = entity.CompletedAt,
        UpdatedTime = entity.UpdatedTime
    };
}

using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai;

public static class AiTaskPlanMapper
{
    public static AiTaskPlanDto MapPlan(
        AiTaskPlanEntity plan,
        IEnumerable<AiTaskPlanItemEntity> items,
        IEnumerable<AiTaskPlanEventEntity>? events = null) => new()
        {
            Id = plan.Id,
            ConversationId = plan.ConversationId,
            RunId = plan.RunId,
            Title = plan.Title,
            Goal = plan.Goal,
            Status = plan.Status,
            Mode = plan.Mode,
            VersionNo = plan.VersionNo,
            Revision = plan.Revision,
            ExecutionStrategy = plan.ExecutionStrategy,
            RisksJson = plan.RisksJson,
            AssumptionsJson = plan.AssumptionsJson,
            MetadataJson = plan.MetadataJson,
            ApprovedBy = plan.ApprovedBy,
            ApprovedRevision = plan.ApprovedRevision,
            ApprovedAt = plan.ApprovedAt,
            CompletedAt = plan.CompletedAt,
            CreatedTime = plan.CreatedTime,
            UpdatedTime = plan.UpdatedTime,
            Progress = BuildProgress(items),
            Items = items.OrderBy(item => item.Depth).ThenBy(item => item.SortOrder).Select(MapItem).ToList(),
            Events = (events ?? []).OrderBy(item => item.Seq).Select(MapEvent).ToList()
        };

    public static AiTaskPlanItemDto MapItem(AiTaskPlanItemEntity item) => new()
    {
        Id = item.Id,
        PlanId = item.PlanId,
        ParentItemId = item.ParentItemId,
        Title = item.Title,
        Description = item.Description,
        Status = item.Status,
        Priority = item.Priority,
        OwnerType = item.OwnerType,
        TaskType = item.TaskType,
        SortOrder = item.SortOrder,
        Depth = item.Depth,
        DependsOnJson = item.DependsOnJson,
        AcceptanceCriteriaJson = item.AcceptanceCriteriaJson,
        ToolCode = item.ToolCode,
        ExecutionHint = item.ExecutionHint,
        Result = item.Result,
        ResultSummary = item.ResultSummary,
        EvidenceJson = item.EvidenceJson,
        ErrorCode = item.ErrorCode,
        ErrorMessage = item.ErrorMessage,
        BlockedReason = item.BlockedReason,
        SkipReason = item.SkipReason,
        RetryCount = item.RetryCount,
        MaxRetryCount = item.MaxRetryCount,
        StartedAt = item.StartedAt,
        CompletedAt = item.CompletedAt,
        UpdatedTime = item.UpdatedTime
    };

    public static AiTaskPlanEventDto MapEvent(AiTaskPlanEventEntity entity) => new()
    {
        Id = entity.Id,
        PlanId = entity.PlanId,
        ItemId = entity.ItemId,
        RunId = entity.RunId,
        Seq = entity.Seq,
        EventName = entity.EventName,
        FromStatus = entity.FromStatus,
        ToStatus = entity.ToStatus,
        Summary = entity.Summary,
        PayloadJson = entity.PayloadJson,
        TraceId = entity.TraceId,
        OperatorUserId = entity.OperatorUserId,
        CreatedTime = entity.CreatedTime
    };

    public static AiTaskPlanItemOutputDto MapOutput(AiTaskPlanItemOutputEntity entity) => new()
    {
        Id = entity.Id,
        PlanId = entity.PlanId,
        ItemId = entity.ItemId,
        RunId = entity.RunId,
        OutputType = entity.OutputType,
        ResultSummary = entity.ResultSummary,
        Content = entity.Content,
        EvidenceJson = entity.EvidenceJson,
        ErrorCode = entity.ErrorCode,
        ErrorMessage = entity.ErrorMessage,
        CreatedTime = entity.CreatedTime
    };

    public static AiTaskPlanProgressDto BuildProgress(IEnumerable<AiTaskPlanItemEntity> source)
    {
        var items = source.ToList();
        var total = items.Count;
        var completed = items.Count(item => item.Status is AiTaskPlanConstants.ItemStatus.Succeeded or AiTaskPlanConstants.ItemStatus.Skipped);
        return new AiTaskPlanProgressDto
        {
            TotalCount = total,
            CompletedCount = completed,
            FailedCount = items.Count(item => item.Status == AiTaskPlanConstants.ItemStatus.Failed),
            BlockedCount = items.Count(item => item.Status == AiTaskPlanConstants.ItemStatus.Blocked),
            WaitingUserCount = items.Count(item => item.Status == AiTaskPlanConstants.ItemStatus.WaitingUser),
            Percent = total == 0 ? 0 : (int)Math.Round(completed * 100m / total)
        };
    }
}

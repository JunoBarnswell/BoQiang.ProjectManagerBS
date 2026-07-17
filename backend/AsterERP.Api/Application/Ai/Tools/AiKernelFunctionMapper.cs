using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.Tools;

public static class AiKernelFunctionMapper
{
    public static AiToolInvocationDto MapInvocation(AiToolExecutionLogEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        RunId = entity.RunId,
        ModelConfigId = entity.ModelConfigId,
        PlanId = entity.PlanId,
        ItemId = entity.ItemId,
        ToolCode = entity.ToolCode ?? string.Empty,
        ToolName = entity.ToolName,
        TraceId = entity.TraceId,
        ArgumentsJson = entity.ArgumentsJson,
        ResultSummary = entity.ResultSummary,
        Status = entity.Status,
        DurationMs = entity.DurationMs,
        ErrorMessage = entity.ErrorMessage,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };
}

using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.Agent;

public sealed class AiAgentExecutionResult
{
    public string PlanId { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public string PlanStatus { get; set; } = AiTaskPlanConstants.PlanStatus.Completed;

    public string Summary { get; set; } = string.Empty;

    public IReadOnlyList<AiTaskPlanEventDto> Events { get; set; } = [];

    public IReadOnlyList<AiTaskPlanItemOutputDto> Outputs { get; set; } = [];
}

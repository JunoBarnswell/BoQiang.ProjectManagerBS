using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.Agent;

public interface IAiAgentExecutionService
{
    Task<AiAgentExecutionResult> ExecuteAsync(
        string planId,
        string runId,
        string? modelConfigId = null,
        string? userInstruction = null,
        IReadOnlyList<string>? enabledToolCodes = null,
        IReadOnlyList<string>? enabledToolDomains = null,
        Func<AiTaskPlanEventDto, CancellationToken, Task>? onEvent = null,
        CancellationToken cancellationToken = default);
}

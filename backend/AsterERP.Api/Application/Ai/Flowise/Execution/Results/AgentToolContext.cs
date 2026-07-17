using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed record AgentToolContext(IReadOnlyList<FlowiseUsedToolDto> UsedTools)
{
    public static AgentToolContext Empty { get; } = new([]);
}

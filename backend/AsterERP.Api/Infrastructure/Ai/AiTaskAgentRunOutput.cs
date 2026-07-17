namespace AsterERP.Api.Infrastructure.Ai;

public sealed class AiTaskAgentRunOutput
{
    public string ResultSummary { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? EvidenceJson { get; set; }

    public string OutputType { get; set; } = "Text";

    public IReadOnlyList<AsterERP.Api.Application.Ai.Tools.AiKernelFunctionGeneratedEvent> Events { get; set; } = [];
}

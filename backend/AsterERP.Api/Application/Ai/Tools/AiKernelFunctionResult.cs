namespace AsterERP.Api.Application.Ai.Tools;

public sealed class AiKernelFunctionResult
{
    public string ResultSummary { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? EvidenceJson { get; set; }

    public string OutputType { get; set; } = "Text";

    public IReadOnlyList<AiKernelFunctionGeneratedEvent> Events { get; set; } = [];
}

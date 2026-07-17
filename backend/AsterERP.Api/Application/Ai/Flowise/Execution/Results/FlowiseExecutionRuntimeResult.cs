using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class FlowiseExecutionRuntimeResult
{
    public object? Action { get; set; }

    public string? ActionJson { get; set; }

    public string Answer { get; set; } = string.Empty;

    public IReadOnlyList<FlowiseAgentExecutedNodeDto> AgentExecutedData { get; set; } = [];

    public IReadOnlyList<FlowiseAgentReasoningDto> AgentReasoning { get; set; } = [];

    public IReadOnlyList<object> Artifacts { get; set; } = [];

    public int EdgeCount { get; set; }

    public IReadOnlyList<string> EntryNodes { get; set; } = [];

    public DateTime ExecutedAt { get; set; }

    public IReadOnlyDictionary<string, object?> FlowState { get; set; } = new Dictionary<string, object?>();

    public int NodeCount { get; set; }

    public string ResourceId { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public IReadOnlyList<FlowiseSourceDocumentDto> SourceDocuments { get; set; } = [];

    public string Status { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;

    public IReadOnlyList<FlowiseUsedToolDto> UsedTools { get; set; } = [];
}

using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class ExecuteFlowNodeResult
{
    public int ExecutionIndex { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string NodeLabel { get; set; } = string.Empty;

    public string SelectedFlowId { get; set; } = string.Empty;

    public string SelectedFlowName { get; set; } = string.Empty;

    public string Input { get; set; } = string.Empty;

    public string ReturnResponseAs { get; set; } = "userMessage";

    public string Content { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<FlowiseSourceDocumentDto> SourceDocuments { get; set; } = [];

    public IReadOnlyList<FlowiseUsedToolDto> UsedTools { get; set; } = [];

    public IReadOnlyList<FlowiseAgentExecutedNodeDto> AgentExecutedData { get; set; } = [];
}

using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class AgentNodeResult
{
    public int ExecutionIndex { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string NodeLabel { get; set; } = string.Empty;

    public IReadOnlyList<AgentMessageDto> Messages { get; set; } = [];

    public string Content { get; set; } = string.Empty;

    public string ReturnResponseAs { get; set; } = "userMessage";

    public DateTime StartedAt { get; set; }

    public DateTime CompletedAt { get; set; }

    public IReadOnlyDictionary<string, object?> StructuredOutput { get; set; } = new Dictionary<string, object?>();

    public string ToolsJson { get; set; } = "[]";

    public string KnowledgeDocumentStoresJson { get; set; } = "[]";

    public string KnowledgeVectorEmbeddingsJson { get; set; } = "[]";

    public IReadOnlyList<FlowiseUsedToolDto> UsedTools { get; set; } = [];

    public IReadOnlyList<FlowiseSourceDocumentDto> SourceDocuments { get; set; } = [];
}

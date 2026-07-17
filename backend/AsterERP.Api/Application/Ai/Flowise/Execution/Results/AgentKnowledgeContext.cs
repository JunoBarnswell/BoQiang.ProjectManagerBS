using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed record AgentKnowledgeContext(IReadOnlyList<FlowiseSourceDocumentDto> SourceDocuments)
{
    public static AgentKnowledgeContext Empty { get; } = new([]);
}

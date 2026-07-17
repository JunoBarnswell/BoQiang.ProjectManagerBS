using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.KnowledgeGraph;

public interface IAiKnowledgeGraphBuildService
{
    Task<AiKnowledgeGraphBuildJobDto> ReindexAsync(AiKnowledgeGraphBuildRequest request, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphBuildJobDto> GetJobAsync(string id, CancellationToken cancellationToken);
}

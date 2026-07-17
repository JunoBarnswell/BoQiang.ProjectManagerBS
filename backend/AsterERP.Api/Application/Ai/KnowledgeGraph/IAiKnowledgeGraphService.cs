using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.KnowledgeGraph;

public interface IAiKnowledgeGraphService
{
    Task<AiKnowledgeGraphOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AiKnowledgeGraphNodeTypeDto>> GetNodeTypesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AiKnowledgeGraphRelationTypeDto>> GetRelationTypesAsync(CancellationToken cancellationToken);

    Task<AiKnowledgeGraphResponse> QueryAsync(AiKnowledgeGraphQueryRequest request, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphResponse> GetNeighborhoodAsync(AiKnowledgeGraphNeighborhoodRequest request, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphPathResponse> FindPathsAsync(AiKnowledgeGraphPathRequest request, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphImpactResponse> AnalyzeImpactAsync(AiKnowledgeGraphImpactRequest request, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphNodeDto> GetNodeAsync(string id, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphNodeDto> CreateNodeAsync(AiKnowledgeGraphNodeUpsertRequest request, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphNodeDto> UpdateNodeAsync(string id, AiKnowledgeGraphNodeUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteNodeAsync(string id, bool cascade, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphEdgeDto> GetEdgeAsync(string id, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphEdgeDto> CreateEdgeAsync(AiKnowledgeGraphEdgeUpsertRequest request, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphEdgeDto> UpdateEdgeAsync(string id, AiKnowledgeGraphEdgeUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteEdgeAsync(string id, CancellationToken cancellationToken);
}

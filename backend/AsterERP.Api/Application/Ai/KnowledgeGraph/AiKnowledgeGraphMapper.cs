using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.KnowledgeGraph;

public static class AiKnowledgeGraphMapper
{
    public static AiKnowledgeGraphNodeTypeDto MapNodeType(AiKnowledgeGraphNodeTypeEntity entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        Color = entity.Color,
        Icon = entity.Icon,
        IsSystem = entity.IsSystem
    };

    public static AiKnowledgeGraphRelationTypeDto MapRelationType(AiKnowledgeGraphRelationTypeEntity entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Directional = entity.Directional,
        Description = entity.Description,
        Color = entity.Color,
        IsSystem = entity.IsSystem
    };

    public static AiKnowledgeGraphNodeDto MapNode(
        AiKnowledgeGraphNodeEntity entity,
        IReadOnlyDictionary<string, AiKnowledgeSourceEntity>? sources = null,
        IReadOnlyDictionary<string, AiKnowledgeDocumentEntity>? documents = null) => new()
    {
        Id = entity.Id,
        NodeKey = entity.NodeKey,
        NodeType = entity.NodeType,
        DisplayName = entity.DisplayName,
        Summary = entity.Summary,
        SourceId = entity.SourceId,
        SourceName = !string.IsNullOrWhiteSpace(entity.SourceId) && sources?.TryGetValue(entity.SourceId, out var source) == true ? source.SourceName : null,
        DocumentId = entity.DocumentId,
        DocumentName = !string.IsNullOrWhiteSpace(entity.DocumentId) && documents?.TryGetValue(entity.DocumentId, out var document) == true ? document.DocumentName : null,
        MetadataJson = entity.MetadataJson,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    public static AiKnowledgeGraphEdgeDto MapEdge(AiKnowledgeGraphEdgeEntity entity) => new()
    {
        Id = entity.Id,
        FromNodeId = entity.FromNodeId,
        ToNodeId = entity.ToNodeId,
        RelationType = entity.RelationType,
        Weight = entity.Weight,
        EvidenceText = entity.EvidenceText,
        SourceId = entity.SourceId,
        MetadataJson = entity.MetadataJson,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    public static AiKnowledgeGraphEvidenceDto MapEvidence(AiKnowledgeGraphEvidenceEntity entity) => new()
    {
        Id = entity.Id,
        NodeId = entity.NodeId,
        EdgeId = entity.EdgeId,
        SourceId = entity.SourceId,
        DocumentId = entity.DocumentId,
        EvidenceText = entity.EvidenceText,
        LocationJson = entity.LocationJson,
        CreatedTime = entity.CreatedTime
    };

    public static AiKnowledgeGraphBuildJobDto MapJob(AiKnowledgeGraphBuildJobEntity entity) => new()
    {
        Id = entity.Id,
        SourceId = entity.SourceId,
        Status = entity.Status,
        Progress = entity.Progress,
        CreatedCount = entity.CreatedCount,
        UpdatedCount = entity.UpdatedCount,
        SkippedCount = entity.SkippedCount,
        ErrorCode = entity.ErrorCode,
        ErrorMessage = entity.ErrorMessage,
        StartedAt = entity.StartedAt,
        FinishedAt = entity.FinishedAt
    };
}

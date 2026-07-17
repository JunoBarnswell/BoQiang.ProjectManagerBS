namespace AsterERP.Contracts.Ai;

public sealed class AiKnowledgeGraphNodeTypeDto
{
    public string Id { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Color { get; set; } = "#2563eb";

    public string Icon { get; set; } = "circle";

    public bool IsSystem { get; set; }
}

public sealed class AiKnowledgeGraphRelationTypeDto
{
    public string Id { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Directional { get; set; } = true;

    public string? Description { get; set; }

    public string Color { get; set; } = "#64748b";

    public bool IsSystem { get; set; }
}

public sealed class AiKnowledgeGraphNodeDto
{
    public string Id { get; set; } = string.Empty;

    public string NodeKey { get; set; } = string.Empty;

    public string NodeType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? SourceId { get; set; }

    public string? SourceName { get; set; }

    public string? DocumentId { get; set; }

    public string? DocumentName { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class AiKnowledgeGraphEdgeDto
{
    public string Id { get; set; } = string.Empty;

    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public string RelationType { get; set; } = string.Empty;

    public decimal Weight { get; set; } = 1;

    public string? EvidenceText { get; set; }

    public string? SourceId { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class AiKnowledgeGraphEvidenceDto
{
    public string Id { get; set; } = string.Empty;

    public string? NodeId { get; set; }

    public string? EdgeId { get; set; }

    public string? SourceId { get; set; }

    public string? DocumentId { get; set; }

    public string EvidenceText { get; set; } = string.Empty;

    public string? LocationJson { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class AiKnowledgeGraphOverviewDto
{
    public int NodeCount { get; set; }

    public int EdgeCount { get; set; }

    public int SourceCount { get; set; }

    public int EvidenceCount { get; set; }

    public AiKnowledgeGraphBuildJobDto? LatestJob { get; set; }

    public DateTime? LastUpdatedTime { get; set; }
}

public sealed class AiKnowledgeGraphResponse
{
    public IReadOnlyList<AiKnowledgeGraphNodeDto> Nodes { get; set; } = [];

    public IReadOnlyList<AiKnowledgeGraphEdgeDto> Edges { get; set; } = [];

    public int TotalNodes { get; set; }

    public int TotalEdges { get; set; }

    public bool Truncated { get; set; }

    public string TraceId { get; set; } = string.Empty;
}

public sealed class AiKnowledgeGraphQueryRequest
{
    public string? Keyword { get; set; }

    public IReadOnlyList<string> SourceIds { get; set; } = [];

    public IReadOnlyList<string> NodeTypes { get; set; } = [];

    public IReadOnlyList<string> RelationTypes { get; set; } = [];

    public int Depth { get; set; } = 1;

    public int Limit { get; set; } = 200;
}

public sealed class AiKnowledgeGraphNeighborhoodRequest
{
    public string NodeId { get; set; } = string.Empty;

    public string Direction { get; set; } = "Both";

    public int Depth { get; set; } = 1;

    public int Limit { get; set; } = 200;
}

public sealed class AiKnowledgeGraphPathRequest
{
    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public IReadOnlyList<string> RelationTypes { get; set; } = [];

    public int MaxDepth { get; set; } = 4;

    public int Limit { get; set; } = 20;
}

public sealed class AiKnowledgeGraphPathDto
{
    public IReadOnlyList<string> NodeIds { get; set; } = [];

    public IReadOnlyList<string> EdgeIds { get; set; } = [];
}

public sealed class AiKnowledgeGraphPathResponse
{
    public IReadOnlyList<AiKnowledgeGraphPathDto> Paths { get; set; } = [];

    public IReadOnlyList<AiKnowledgeGraphNodeDto> Nodes { get; set; } = [];

    public IReadOnlyList<AiKnowledgeGraphEdgeDto> Edges { get; set; } = [];

    public bool Truncated { get; set; }
}

public sealed class AiKnowledgeGraphImpactRequest
{
    public string NodeId { get; set; } = string.Empty;

    public string Direction { get; set; } = "Outgoing";

    public int MaxDepth { get; set; } = 4;

    public int Limit { get; set; } = 200;
}

public sealed class AiKnowledgeGraphImpactResponse
{
    public string RootNodeId { get; set; } = string.Empty;

    public IReadOnlyList<AiKnowledgeGraphNodeDto> Nodes { get; set; } = [];

    public IReadOnlyList<AiKnowledgeGraphEdgeDto> Edges { get; set; } = [];

    public bool Truncated { get; set; }
}

public sealed class AiKnowledgeGraphNodeUpsertRequest
{
    public string NodeKey { get; set; } = string.Empty;

    public string NodeType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? SourceId { get; set; }

    public string? DocumentId { get; set; }

    public string? MetadataJson { get; set; }
}

public class AiKnowledgeGraphEdgeUpsertRequest
{
    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public string RelationType { get; set; } = string.Empty;

    public decimal Weight { get; set; } = 1;

    public string? EvidenceText { get; set; }

    public string? SourceId { get; set; }

    public string? MetadataJson { get; set; }
}

public sealed class AiKnowledgeGraphBuildRequest
{
    public string? SourceId { get; set; }

    public IReadOnlyList<string> DocumentIds { get; set; } = [];

    public string Mode { get; set; } = "Upsert";

    public string? RequestId { get; set; }
}

public sealed class AiKnowledgeGraphBuildJobDto
{
    public string Id { get; set; } = string.Empty;

    public string? SourceId { get; set; }

    public string Status { get; set; } = "Pending";

    public int Progress { get; set; }

    public int CreatedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int SkippedCount { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }
}

public sealed class AiKnowledgeGraphImportRequest
{
    public string? SourceId { get; set; }

    public string Mode { get; set; } = "Upsert";

    public string? RequestId { get; set; }

    public IReadOnlyList<AiKnowledgeGraphNodeUpsertRequest> Nodes { get; set; } = [];

    public IReadOnlyList<AiKnowledgeGraphEdgeImportRequest> Edges { get; set; } = [];
}

public sealed class AiKnowledgeGraphEdgeImportRequest : AiKnowledgeGraphEdgeUpsertRequest
{
    public string? FromNodeKey { get; set; }

    public string? ToNodeKey { get; set; }
}

public sealed class AiKnowledgeGraphImportResultDto
{
    public int CreatedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int SkippedCount { get; set; }
}

public sealed class AiKnowledgeGraphExportRequest
{
    public IReadOnlyList<string> SourceIds { get; set; } = [];

    public IReadOnlyList<string> NodeTypes { get; set; } = [];

    public IReadOnlyList<string> RelationTypes { get; set; } = [];

    public bool IncludeEvidence { get; set; } = true;
}

public sealed class AiKnowledgeGraphExportDto
{
    public IReadOnlyList<AiKnowledgeGraphNodeDto> Nodes { get; set; } = [];

    public IReadOnlyList<AiKnowledgeGraphEdgeDto> Edges { get; set; } = [];

    public IReadOnlyList<AiKnowledgeGraphEvidenceDto> Evidence { get; set; } = [];

    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
}

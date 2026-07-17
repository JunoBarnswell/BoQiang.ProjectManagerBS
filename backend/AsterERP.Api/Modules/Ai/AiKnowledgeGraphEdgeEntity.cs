using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_knowledge_graph_edges")]
public sealed class AiKnowledgeGraphEdgeEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? SourceId { get; set; }

    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public string RelationType { get; set; } = string.Empty;

    public decimal Weight { get; set; } = 1;

    [SugarColumn(IsNullable = true)]
    public string? EvidenceText { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? MetadataJson { get; set; }
}

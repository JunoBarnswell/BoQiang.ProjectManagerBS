using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_knowledge_graph_evidence")]
public sealed class AiKnowledgeGraphEvidenceEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? SourceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DocumentId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? NodeId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? EdgeId { get; set; }

    public string EvidenceText { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? LocationJson { get; set; }
}

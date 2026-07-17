using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_knowledge_graph_nodes")]
public sealed class AiKnowledgeGraphNodeEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? SourceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DocumentId { get; set; }

    public string NodeKey { get; set; } = string.Empty;

    public string NodeType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Summary { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? MetadataJson { get; set; }
}

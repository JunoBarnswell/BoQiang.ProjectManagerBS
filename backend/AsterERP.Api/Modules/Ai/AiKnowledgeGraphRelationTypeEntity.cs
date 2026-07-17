using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_knowledge_graph_relation_types")]
public sealed class AiKnowledgeGraphRelationTypeEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Directional { get; set; } = true;

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    public string Color { get; set; } = "#64748b";

    public bool IsSystem { get; set; }
}

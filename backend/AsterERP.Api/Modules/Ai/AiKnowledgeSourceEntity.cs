using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_knowledge_sources")]
public sealed class AiKnowledgeSourceEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string SourceCode { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public string SourceType { get; set; } = "Document";

    public string Status { get; set; } = "Disabled";

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }
}

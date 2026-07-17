using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_knowledge_documents")]
public sealed class AiKnowledgeDocumentEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string DocumentName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? StoragePath { get; set; }

    public string IndexStatus { get; set; } = "Pending";

    public int ChunkCount { get; set; }
}

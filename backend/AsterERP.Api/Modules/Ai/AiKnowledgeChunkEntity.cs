using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_knowledge_chunks")]
public sealed class AiKnowledgeChunkEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? MetadataJson { get; set; }
}

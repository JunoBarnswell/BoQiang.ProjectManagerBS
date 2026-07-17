using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_document_store_chunks")]
public sealed class FlowiseDocumentStoreChunkEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string StoreId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public int TokenCount { get; set; }
}

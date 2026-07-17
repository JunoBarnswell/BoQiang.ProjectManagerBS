using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_vector_store_configs")]
public sealed class FlowiseVectorStoreConfigEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string StoreId { get; set; } = string.Empty;

    public string VectorProvider { get; set; } = string.Empty;

    public string EmbeddingProvider { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? RecordManagerProvider { get; set; }

    public string VectorStoreConfigJson { get; set; } = "{}";
}

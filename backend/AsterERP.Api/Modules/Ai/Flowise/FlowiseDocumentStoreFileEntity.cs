using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_document_store_files")]
public sealed class FlowiseDocumentStoreFileEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string StoreId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string LoaderType { get; set; } = string.Empty;

    public string LoaderConfigJson { get; set; } = "{}";

    public string Status { get; set; } = "Pending";
}

using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_document_store_upsert_history")]
public sealed class FlowiseDocumentStoreUpsertHistoryEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string StoreId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? LoaderId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ChatflowId { get; set; }

    public string Status { get; set; } = "Completed";

    public int ProcessedCount { get; set; }

    public int AddedCount { get; set; }

    public int ReplacedCount { get; set; }

    public int SkippedCount { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    public string RequestJson { get; set; } = "{}";

    public string ResultJson { get; set; } = "{}";
}

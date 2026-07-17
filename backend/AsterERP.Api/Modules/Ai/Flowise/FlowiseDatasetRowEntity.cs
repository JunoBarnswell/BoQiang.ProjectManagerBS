using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_dataset_rows")]
public sealed class FlowiseDatasetRowEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string DatasetId { get; set; } = string.Empty;

    public string Input { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ExpectedOutput { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ActualOutput { get; set; }

    public string MetadataJson { get; set; } = "{}";
}

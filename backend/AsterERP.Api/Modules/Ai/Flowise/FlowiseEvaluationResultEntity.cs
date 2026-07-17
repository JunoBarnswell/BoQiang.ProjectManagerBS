using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_evaluation_results")]
public sealed class FlowiseEvaluationResultEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string EvaluationId { get; set; } = string.Empty;

    public int VersionNo { get; set; }

    public string Status { get; set; } = "Pending";

    public decimal PassRate { get; set; }

    public int AverageLatencyMs { get; set; }

    public int TotalTokens { get; set; }

    public string MetricsJson { get; set; } = "{}";

    public string ResultRowsJson { get; set; } = "[]";
}

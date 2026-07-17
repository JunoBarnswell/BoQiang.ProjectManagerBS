using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_executions")]
public sealed class FlowiseExecutionEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string ResourceId { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public string FlowType { get; set; } = "Chatflow";

    public string Status { get; set; } = "Queued";

    public string InputJson { get; set; } = "{}";

    public string OutputJson { get; set; } = "{}";

    public string SourceDocumentsJson { get; set; } = "[]";

    [SugarColumn(IsNullable = true)]
    public string? ActionJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    public string TraceId { get; set; } = string.Empty;

    public int DurationMs { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? StartedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? IdempotencyKey { get; set; }
}

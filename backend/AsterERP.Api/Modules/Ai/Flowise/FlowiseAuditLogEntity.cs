using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_audit_logs")]
public sealed class FlowiseAuditLogEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ResourceId { get; set; }

    public string DetailJson { get; set; } = "{}";
}

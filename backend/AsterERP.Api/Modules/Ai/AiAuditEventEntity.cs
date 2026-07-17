using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_audit_events")]
public sealed class AiAuditEventEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ResourceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? UserId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DetailJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? TraceId { get; set; }
}

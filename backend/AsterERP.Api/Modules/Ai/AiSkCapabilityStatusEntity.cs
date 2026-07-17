using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_sk_capability_status")]
public sealed class AiSkCapabilityStatusEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string CapabilityCode { get; set; } = string.Empty;

    public string Status { get; set; } = "Blocked";

    public string FrameworkType { get; set; } = string.Empty;

    public string ImplementationSymbol { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

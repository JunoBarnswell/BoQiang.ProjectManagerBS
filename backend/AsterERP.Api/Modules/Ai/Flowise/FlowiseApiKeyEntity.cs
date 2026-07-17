using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_api_keys")]
public sealed class FlowiseApiKeyEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string ApiKeyCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    public string Status { get; set; } = "Enabled";

    public string KeyHash { get; set; } = string.Empty;

    public string KeyMask { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    [SugarColumn(IsNullable = true)]
    public DateTime? LastUsedAt { get; set; }
}

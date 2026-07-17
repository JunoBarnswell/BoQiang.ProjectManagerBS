using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_sso_configs")]
public sealed class FlowiseSsoConfigEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string ConfigKey { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    public string Status { get; set; } = "Enabled";

    public bool Enabled { get; set; }

    public string SettingsJson { get; set; } = "{}";

    public string MetadataJson { get; set; } = "{}";
}

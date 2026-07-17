using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_account_settings")]
public sealed class FlowiseAccountSettingEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Email { get; set; }

    public string PreferencesJson { get; set; } = "{}";
}

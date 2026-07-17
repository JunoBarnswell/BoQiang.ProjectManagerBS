using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_users")]
public sealed class FlowiseUserEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string UserKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Email { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    public string Status { get; set; } = "Enabled";

    public string RolesJson { get; set; } = "[]";

    public string WorkspaceIdsJson { get; set; } = "[]";

    public string MetadataJson { get; set; } = "{}";
}

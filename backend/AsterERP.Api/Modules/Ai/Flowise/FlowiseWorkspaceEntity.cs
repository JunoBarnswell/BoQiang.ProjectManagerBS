using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_workspaces")]
public sealed class FlowiseWorkspaceEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string WorkspaceKey { get; set; } = string.Empty;

    public string WorkspaceName { get; set; } = string.Empty;

    public string Status { get; set; } = "Enabled";

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }
}

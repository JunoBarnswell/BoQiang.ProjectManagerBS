using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_node_definitions")]
public sealed class FlowiseNodeDefinitionEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string NodeType { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Icon { get; set; }

    public int Version { get; set; } = 1;

    public string DefinitionJson { get; set; } = "{}";
}

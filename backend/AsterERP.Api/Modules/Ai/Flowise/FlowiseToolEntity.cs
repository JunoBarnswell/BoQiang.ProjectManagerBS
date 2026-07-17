using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_tools")]
public sealed class FlowiseToolEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string ToolKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ToolType { get; set; }

    public string Status { get; set; } = "Enabled";

    public string SchemaJson { get; set; } = "{}";

    public string ImplementationJson { get; set; } = "{}";

    public string MetadataJson { get; set; } = "{}";
}

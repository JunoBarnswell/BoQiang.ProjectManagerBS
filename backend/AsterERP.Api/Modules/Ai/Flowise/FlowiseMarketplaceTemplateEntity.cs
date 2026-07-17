using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_marketplace_templates")]
public sealed class FlowiseMarketplaceTemplateEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Category { get; set; }

    public string Status { get; set; } = "Enabled";

    public string FlowData { get; set; } = "{}";

    public string MetadataJson { get; set; } = "{}";
}

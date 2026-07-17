using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_variables")]
public sealed class FlowiseVariableEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string VariableKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Scope { get; set; }

    public bool IsSecret { get; set; }

    public string Status { get; set; } = "Enabled";

    public string ValueJson { get; set; } = "{}";

    public string MetadataJson { get; set; } = "{}";

    [SugarColumn(IsNullable = true)]
    public string? SecretCipherText { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SecretHash { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SecretMask { get; set; }
}

using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_credentials")]
public sealed class FlowiseCredentialEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string CredentialKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? CredentialType { get; set; }

    public string Status { get; set; } = "Enabled";

    public string ConfigJson { get; set; } = "{}";

    public string MetadataJson { get; set; } = "{}";

    [SugarColumn(IsNullable = true)]
    public string? SecretCipherText { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SecretHash { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SecretMask { get; set; }
}

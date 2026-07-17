using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_secret_refs")]
public sealed class AiSecretRefEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string SecretCode { get; set; } = string.Empty;

    public string SecretType { get; set; } = "ApiKey";

    public string CipherText { get; set; } = string.Empty;

    public string Mask { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ResourceType { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ResourceId { get; set; }
}

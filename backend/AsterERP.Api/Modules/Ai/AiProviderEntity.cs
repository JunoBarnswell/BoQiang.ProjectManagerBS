using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_providers")]
public sealed class AiProviderEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ProviderCode { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ProtocolType { get; set; } = "OpenAiCompatible";

    public string BaseUrl { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ApiKeyCipherText { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ApiKeyMask { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 120;

    [SugarColumn(IsNullable = true)]
    public string? ExtraParametersJson { get; set; }
}

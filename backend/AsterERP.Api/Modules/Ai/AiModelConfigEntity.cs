using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_model_configs")]
public sealed class AiModelConfigEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int MaxContextTokens { get; set; } = 64000;

    public int MaxOutputTokens { get; set; } = 8192;

    [SugarColumn(IsNullable = true)]
    public decimal? DefaultTemperature { get; set; }

    [SugarColumn(IsNullable = true)]
    public decimal? DefaultTopP { get; set; }

    public bool ThinkingEnabledDefault { get; set; } = true;

    [SugarColumn(IsNullable = true)]
    public string? ReasoningEffort { get; set; }

    public bool ToolStreamEnabledDefault { get; set; }

    public int MaxParallelRuns { get; set; } = 3;

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}

using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_usage_logs")]
public sealed class AiUsageLogEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ConversationId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RunId { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int ReasoningTokens { get; set; }

    public int TotalTokens { get; set; }

    public decimal CostAmount { get; set; }

    public int DurationMs { get; set; }

    public bool IsSuccess { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    public DateTime RequestStartedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? RequestCompletedAt { get; set; }
}

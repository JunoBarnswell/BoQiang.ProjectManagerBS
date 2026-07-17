using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_chat_runs")]
public sealed class AiChatRunEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? UserMessageId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AssistantMessageId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ProviderId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ModelConfigId { get; set; }

    public string Mode { get; set; } = "Single";

    public string Status { get; set; } = "Queued";

    [SugarColumn(IsNullable = true)]
    public string? ClientMessageId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? IdempotencyKey { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RequestHash { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AgentProfileIdsJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? CoordinatorAgentProfileId { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int ReasoningTokens { get; set; }

    public int TotalTokens { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? TraceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? StartedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedAt { get; set; }
}

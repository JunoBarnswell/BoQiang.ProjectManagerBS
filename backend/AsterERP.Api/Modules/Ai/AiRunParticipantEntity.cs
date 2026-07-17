using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_run_participants")]
public sealed class AiRunParticipantEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public string AgentProfileId { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;

    public string Status { get; set; } = "Queued";

    [SugarColumn(IsNullable = true)]
    public string? DraftMessageId { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int ReasoningTokens { get; set; }

    public int TotalTokens { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }
}

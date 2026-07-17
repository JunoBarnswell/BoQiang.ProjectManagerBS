using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_messages")]
public sealed class AiMessageEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? RunId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ParentMessageId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AgentProfileId { get; set; }

    public string Role { get; set; } = string.Empty;

    public int Seq { get; set; }

    public string Content { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ReasoningContent { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ToolCallsJson { get; set; }

    public int TokenCount { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? FinishReason { get; set; }

    public string Status { get; set; } = "Completed";

    [SugarColumn(IsNullable = true)]
    public string? MetadataJson { get; set; }
}

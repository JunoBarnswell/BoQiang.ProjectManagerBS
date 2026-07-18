using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Im;

[SugarTable("im_conversations")]
public sealed class ImConversationEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string ConversationKey { get; set; } = string.Empty;

    /// <summary>
    /// Direct keeps the existing two-party semantics. Group represents a conversation whose
    /// effective access list is exclusively held by ImConversationParticipantEntity.
    /// </summary>
    public string ConversationType { get; set; } = "Direct";

    [SugarColumn(IsNullable = true)]
    public string? Title { get; set; }

    /// <summary>
    /// Archived conversations retain their message history but reject new messages.
    /// </summary>
    public string Status { get; set; } = "Active";

    // Legacy direct-conversation projection. Group conversations intentionally leave these
    // values empty; participant rows are the single source of truth for group membership.
    public string ParticipantAUserId { get; set; } = string.Empty;

    public string ParticipantBUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? LastMessageId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? LastMessagePreview { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastMessageAt { get; set; }
}

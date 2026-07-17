using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Im;

[SugarTable("im_conversation_participants")]
public sealed class ImConversationParticipantEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public int UnreadCount { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? LastReadMessageId { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastReadAt { get; set; }
}

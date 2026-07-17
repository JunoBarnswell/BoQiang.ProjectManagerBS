using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Im;

[SugarTable("im_conversations")]
public sealed class ImConversationEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string ConversationKey { get; set; } = string.Empty;

    public string ParticipantAUserId { get; set; } = string.Empty;

    public string ParticipantBUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? LastMessageId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? LastMessagePreview { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastMessageAt { get; set; }
}

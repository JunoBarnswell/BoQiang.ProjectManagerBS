using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Im;

[SugarTable("im_messages")]
public sealed class ImMessageEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string SenderUserId { get; set; } = string.Empty;

    public string ReceiverUserId { get; set; } = string.Empty;

    public string MessageType { get; set; } = "Text";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string Content { get; set; } = string.Empty;

    public string Status { get; set; } = "Sent";

    [SugarColumn(IsNullable = true)]
    public string? ClientMessageId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SourceAppCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? CloudImMessageId { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

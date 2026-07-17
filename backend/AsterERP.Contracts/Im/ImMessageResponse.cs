namespace AsterERP.Contracts.Im;

public sealed record ImMessageResponse(
    string Id,
    string ConversationId,
    string SenderUserId,
    string ReceiverUserId,
    string MessageType,
    string Content,
    string Status,
    string? ClientMessageId,
    string? SourceAppCode,
    DateTime SentAt);

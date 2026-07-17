namespace AsterERP.Contracts.Im;

public sealed record ImSendMessageRequest(
    string Content,
    string? ClientMessageId,
    string? MessageType,
    string? SourceAppCode);

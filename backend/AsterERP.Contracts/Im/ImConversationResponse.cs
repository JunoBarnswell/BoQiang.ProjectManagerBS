namespace AsterERP.Contracts.Im;

public sealed record ImConversationResponse(
    string Id,
    string TenantId,
    string ConversationKey,
    string PeerUserId,
    string PeerDisplayName,
    string? LastMessageId,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    int UnreadCount,
    DateTime CreatedTime);

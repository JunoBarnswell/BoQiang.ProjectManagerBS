namespace AsterERP.Contracts.Im;

public sealed record ImConversationResponse(
    string Id,
    string TenantId,
    string ConversationKey,
    string ConversationType,
    string? Title,
    string Status,
    string PeerUserId,
    string PeerDisplayName,
    string? LastMessageId,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    int UnreadCount,
    int ParticipantCount,
    DateTime CreatedTime);

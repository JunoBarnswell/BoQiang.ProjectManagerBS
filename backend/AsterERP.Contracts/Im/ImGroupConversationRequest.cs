namespace AsterERP.Contracts.Im;

/// <summary>
/// Internal application contract for an idempotent group conversation. The caller owns the
/// source relation and passes the complete effective member set on every synchronization.
/// </summary>
public sealed record ImGroupConversationRequest(
    string ConversationKey,
    string Title,
    IReadOnlyCollection<string> ParticipantUserIds);

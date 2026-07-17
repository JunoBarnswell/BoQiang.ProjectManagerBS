namespace AsterERP.Contracts.Im;

public sealed record ImUnreadSummaryResponse(
    int TotalUnread,
    IReadOnlyDictionary<string, int> ConversationUnreadCounts);

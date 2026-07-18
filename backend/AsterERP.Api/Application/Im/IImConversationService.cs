using AsterERP.Contracts.Im;

namespace AsterERP.Api.Application.Im;

public interface IImConversationService
{
    Task<IReadOnlyList<ImConversationResponse>> GetConversationsAsync(CancellationToken cancellationToken = default);

    Task<ImConversationResponse> CreateDirectConversationAsync(string targetUserId, CancellationToken cancellationToken = default);

    Task<ImConversationResponse> EnsureGroupConversationAsync(ImGroupConversationRequest request, CancellationToken cancellationToken = default);

    Task SynchronizeGroupParticipantsAsync(string conversationId, IReadOnlyCollection<string> participantUserIds, CancellationToken cancellationToken = default);

    Task ArchiveGroupConversationAsync(string conversationId, CancellationToken cancellationToken = default);

    Task ActivateGroupConversationAsync(string conversationId, CancellationToken cancellationToken = default);

    Task<ImMessagePageResponse> GetMessagesAsync(string conversationId, string? cursor, int take, CancellationToken cancellationToken = default);

    Task<ImMessageResponse> SendMessageAsync(string conversationId, ImSendMessageRequest request, CancellationToken cancellationToken = default);

    Task<ImUnreadSummaryResponse> MarkReadAsync(string conversationId, CancellationToken cancellationToken = default);

    Task<ImUnreadSummaryResponse> GetUnreadSummaryAsync(CancellationToken cancellationToken = default);
}

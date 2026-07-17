using AsterERP.Contracts.Ai;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai;

public interface IAiConversationService
{
    Task<GridPageResult<AiConversationDto>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<AiConversationDetailDto> GetDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<AiConversationDto> CreateAsync(AiConversationCreateRequest request, CancellationToken cancellationToken = default);

    Task<AiConversationDto> UpdateAsync(string id, AiConversationUpdateRequest request, CancellationToken cancellationToken = default);

    Task<AiConversationDto> UpdateStatusAsync(string id, AiConversationStatusRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<GridPageResult<AiMessageDto>> GetMessagesAsync(string conversationId, GridQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiContextSnapshotDto>> GetSnapshotsAsync(string conversationId, CancellationToken cancellationToken = default);

    Task<AiContextSnapshotDto> CompressAsync(string conversationId, string? modelConfigId, CancellationToken cancellationToken = default);

    Task<bool> FeedbackAsync(string messageId, AiMessageFeedbackRequest request, CancellationToken cancellationToken = default);
}

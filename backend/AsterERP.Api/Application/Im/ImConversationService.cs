using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Im;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Im;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.Im;

public sealed class ImConversationService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IClock clock,
    IImAccountBindingService accountBindingService,
    IImRealtimePushService realtimePushService) : IImConversationService
{
    private const int MaxMessageTake = 100;

    public async Task<IReadOnlyList<ImConversationResponse>> GetConversationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var db = databaseAccessor.MainDb;
        var participants = await db.Queryable<ImConversationParticipantEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.UserId == userId)
            .ToListAsync(cancellationToken);
        if (participants.Count == 0)
        {
            return [];
        }

        var conversationIds = participants.Select(item => item.ConversationId).ToList();
        var conversations = await db.Queryable<ImConversationEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && conversationIds.Contains(item.Id))
            .OrderBy(item => item.LastMessageAt, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        var peerIds = conversations.Select(item => GetPeerUserId(item, userId)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var userMap = await ResolveUserNamesAsync(peerIds, cancellationToken);
        var participantMap = participants.ToDictionary(item => item.ConversationId, StringComparer.OrdinalIgnoreCase);

        return conversations
            .Select(item =>
            {
                var peerUserId = GetPeerUserId(item, userId);
                var peerName = userMap.TryGetValue(peerUserId, out var peerNameValue)
                    ? peerNameValue
                    : peerUserId;
                return new ImConversationResponse(
                    item.Id,
                    item.TenantId,
                    item.ConversationKey,
                    peerUserId,
                    peerName,
                    item.LastMessageId,
                    item.LastMessagePreview,
                    item.LastMessageAt,
                    participantMap.TryGetValue(item.Id, out var participant) ? participant.UnreadCount : 0,
                    item.CreatedTime);
            })
            .ToList();
    }

    private async Task<Dictionary<string, string>> ResolveUserNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var appCode = currentUser.GetAsterErpAppCode();
        var db = string.IsNullOrWhiteSpace(appCode) || string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase)
            ? databaseAccessor.MainDb
            : await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var users = await db.Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted && userIds.Contains(item.Id))
            .Select(item => new { item.Id, item.UserName, item.DisplayName })
            .ToListAsync(cancellationToken);
        return users.ToDictionary(
            item => item.Id,
            item => string.IsNullOrWhiteSpace(item.DisplayName) ? item.UserName : item.DisplayName,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ImConversationResponse> CreateDirectConversationAsync(string targetUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var normalizedTargetUserId = NormalizeRequired(targetUserId, "目标用户不能为空");
        if (string.Equals(userId, normalizedTargetUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("不能与自己创建会话", ErrorCodes.ParameterInvalid);
        }

        await accountBindingService.EnsureForUserAsync(tenantId, userId, cancellationToken);
        await accountBindingService.EnsureForUserAsync(tenantId, normalizedTargetUserId, cancellationToken);

        var db = databaseAccessor.MainDb;
        var key = BuildConversationKey(tenantId, userId, normalizedTargetUserId);
        var conversation = await db.Queryable<ImConversationEntity>()
            .FirstAsync(item => !item.IsDeleted && item.TenantId == tenantId && item.ConversationKey == key, cancellationToken);
        if (conversation is null)
        {
            conversation = CreateConversation(tenantId, userId, normalizedTargetUserId, key);
            db.Ado.BeginTran();
            try
            {
                await db.Insertable(conversation).ExecuteCommandAsync(cancellationToken);
                await db.Insertable(CreateParticipant(tenantId, conversation.Id, userId)).ExecuteCommandAsync(cancellationToken);
                await db.Insertable(CreateParticipant(tenantId, conversation.Id, normalizedTargetUserId)).ExecuteCommandAsync(cancellationToken);
                db.Ado.CommitTran();
            }
            catch
            {
                db.Ado.RollbackTran();
                conversation = await db.Queryable<ImConversationEntity>()
                    .FirstAsync(item => !item.IsDeleted && item.TenantId == tenantId && item.ConversationKey == key, cancellationToken);
                if (conversation is null)
                {
                    throw;
                }
            }
        }

        var conversations = await GetConversationsAsync(cancellationToken);
        return conversations.First(item => item.Id == conversation.Id);
    }

    public async Task<ImMessagePageResponse> GetMessagesAsync(string conversationId, string? cursor, int take, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var normalizedConversationId = NormalizeRequired(conversationId, "会话不能为空");
        var db = databaseAccessor.MainDb;
        await RequireParticipantAsync(db, tenantId, normalizedConversationId, userId, cancellationToken);
        var pageSize = Math.Clamp(take, 1, MaxMessageTake);
        DateTime? before = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var cursorMessage = await db.Queryable<ImMessageEntity>()
                .FirstAsync(item => !item.IsDeleted && item.TenantId == tenantId && item.ConversationId == normalizedConversationId && item.Id == cursor, cancellationToken);
            before = cursorMessage?.SentAt;
        }

        var rows = await db.Queryable<ImMessageEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.ConversationId == normalizedConversationId)
            .WhereIF(before.HasValue, item => item.SentAt < before!.Value)
            .OrderBy(item => item.SentAt, OrderByType.Desc)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);
        var hasMore = rows.Count > pageSize;
        var items = rows.Take(pageSize).Reverse().Select(MapMessage).ToList();
        return new ImMessagePageResponse(items, hasMore ? items.FirstOrDefault()?.Id : null, hasMore);
    }

    public async Task<ImMessageResponse> SendMessageAsync(string conversationId, ImSendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var normalizedConversationId = NormalizeRequired(conversationId, "会话不能为空");
        var content = NormalizeContent(request.Content);
        var db = databaseAccessor.MainDb;
        var conversation = await RequireConversationAsync(db, tenantId, normalizedConversationId, cancellationToken);
        await RequireParticipantAsync(db, tenantId, normalizedConversationId, userId, cancellationToken);
        var receiverUserId = GetPeerUserId(conversation, userId);

        if (!string.IsNullOrWhiteSpace(request.ClientMessageId))
        {
            var existing = await db.Queryable<ImMessageEntity>()
                .FirstAsync(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.ConversationId == normalizedConversationId &&
                    item.SenderUserId == userId &&
                    item.ClientMessageId == request.ClientMessageId.Trim(), cancellationToken);
            if (existing is not null)
            {
                return MapMessage(existing);
            }
        }

        var message = new ImMessageEntity
        {
            TenantId = tenantId,
            ConversationId = normalizedConversationId,
            SenderUserId = userId,
            ReceiverUserId = receiverUserId,
            MessageType = NormalizeMessageType(request.MessageType),
            Content = content,
            Status = "Sent",
            ClientMessageId = NormalizeOptional(request.ClientMessageId),
            SourceAppCode = NormalizeOptional(request.SourceAppCode ?? currentUser.GetAsterErpAppCode()),
            SentAt = clock.Now,
            CreatedBy = userId
        };

        db.Ado.BeginTran();
        try
        {
            await db.Insertable(message).ExecuteCommandAsync(cancellationToken);
            conversation.LastMessageId = message.Id;
            conversation.LastMessagePreview = BuildPreview(content);
            conversation.LastMessageAt = message.SentAt;
            conversation.UpdatedBy = userId;
            conversation.UpdatedTime = clock.Now;
            await db.Updateable(conversation).ExecuteCommandAsync(cancellationToken);
            await db.Updateable<ImConversationParticipantEntity>()
                .SetColumns(item => item.UnreadCount == item.UnreadCount + 1)
                .SetColumns(item => item.UpdatedTime == clock.Now)
                .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.ConversationId == normalizedConversationId && item.UserId == receiverUserId)
                .ExecuteCommandAsync(cancellationToken);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            if (!string.IsNullOrWhiteSpace(message.ClientMessageId))
            {
                var existing = await db.Queryable<ImMessageEntity>()
                    .FirstAsync(item =>
                        !item.IsDeleted &&
                        item.TenantId == tenantId &&
                        item.ConversationId == normalizedConversationId &&
                        item.SenderUserId == userId &&
                        item.ClientMessageId == message.ClientMessageId, cancellationToken);
                if (existing is not null)
                {
                    return MapMessage(existing);
                }
            }

            throw;
        }

        var response = MapMessage(message);
        await realtimePushService.PushMessageAsync(tenantId, receiverUserId, response, cancellationToken);
        await realtimePushService.PushUnreadChangedAsync(tenantId, receiverUserId, await GetUnreadSummaryForUserAsync(tenantId, receiverUserId, cancellationToken), cancellationToken);
        return response;
    }

    public async Task<ImUnreadSummaryResponse> MarkReadAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var normalizedConversationId = NormalizeRequired(conversationId, "会话不能为空");
        var db = databaseAccessor.MainDb;
        await RequireParticipantAsync(db, tenantId, normalizedConversationId, userId, cancellationToken);
        var lastMessageId = await db.Queryable<ImMessageEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.ConversationId == normalizedConversationId)
            .OrderBy(item => item.SentAt, OrderByType.Desc)
            .Select(item => item.Id)
            .FirstAsync(cancellationToken);
        await db.Updateable<ImConversationParticipantEntity>()
            .SetColumns(item => item.UnreadCount == 0)
            .SetColumns(item => item.LastReadMessageId == lastMessageId)
            .SetColumns(item => item.LastReadAt == clock.Now)
            .SetColumns(item => item.UpdatedTime == clock.Now)
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.ConversationId == normalizedConversationId && item.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
        var summary = await GetUnreadSummaryAsync(cancellationToken);
        await realtimePushService.PushUnreadChangedAsync(tenantId, userId, summary, cancellationToken);
        return summary;
    }

    public Task<ImUnreadSummaryResponse> GetUnreadSummaryAsync(CancellationToken cancellationToken = default) =>
        GetUnreadSummaryForUserAsync(RequireTenantId(), RequireUserId(), cancellationToken);

    private async Task<ImUnreadSummaryResponse> GetUnreadSummaryForUserAsync(string tenantId, string userId, CancellationToken cancellationToken)
    {
        var rows = await databaseAccessor.MainDb.Queryable<ImConversationParticipantEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.UserId == userId)
            .ToListAsync(cancellationToken);
        return new ImUnreadSummaryResponse(
            rows.Sum(item => item.UnreadCount),
            rows.ToDictionary(item => item.ConversationId, item => item.UnreadCount, StringComparer.OrdinalIgnoreCase));
    }

    private async Task RequireParticipantAsync(ISqlSugarClient db, string tenantId, string conversationId, string userId, CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<ImConversationParticipantEntity>()
            .AnyAsync(item => !item.IsDeleted && item.TenantId == tenantId && item.ConversationId == conversationId && item.UserId == userId, cancellationToken);
        if (!exists)
        {
            throw new ValidationException("无权访问该会话", ErrorCodes.PermissionDenied);
        }
    }

    private static async Task<ImConversationEntity> RequireConversationAsync(ISqlSugarClient db, string tenantId, string conversationId, CancellationToken cancellationToken)
    {
        return await db.Queryable<ImConversationEntity>()
            .FirstAsync(item => !item.IsDeleted && item.TenantId == tenantId && item.Id == conversationId, cancellationToken)
            ?? throw new ValidationException("会话不存在", ErrorCodes.ParameterInvalid);
    }

    private string RequireTenantId() =>
        NormalizeRequired(currentUser.GetAsterErpTenantId(), "当前租户不能为空");

    private string RequireUserId() =>
        NormalizeRequired(currentUser.GetAsterErpUserId(), "当前用户不能为空");

    private static string BuildConversationKey(string tenantId, string userId, string targetUserId)
    {
        var ordered = new[] { userId.Trim(), targetUserId.Trim() }.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return $"direct:{tenantId.Trim()}:{ordered[0]}:{ordered[1]}";
    }

    private static ImConversationEntity CreateConversation(string tenantId, string userId, string targetUserId, string key)
    {
        var ordered = new[] { userId.Trim(), targetUserId.Trim() }.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return new ImConversationEntity
        {
            TenantId = tenantId,
            ConversationKey = key,
            ParticipantAUserId = ordered[0],
            ParticipantBUserId = ordered[1],
            CreatedBy = userId
        };
    }

    private static ImConversationParticipantEntity CreateParticipant(string tenantId, string conversationId, string userId) =>
        new()
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            UserId = userId,
            CreatedBy = userId
        };

    private static string GetPeerUserId(ImConversationEntity conversation, string userId) =>
        string.Equals(conversation.ParticipantAUserId, userId, StringComparison.OrdinalIgnoreCase)
            ? conversation.ParticipantBUserId
            : conversation.ParticipantAUserId;

    private static ImMessageResponse MapMessage(ImMessageEntity entity) =>
        new(
            entity.Id,
            entity.ConversationId,
            entity.SenderUserId,
            entity.ReceiverUserId,
            entity.MessageType,
            entity.Content,
            entity.Status,
            entity.ClientMessageId,
            entity.SourceAppCode,
            entity.SentAt);

    private static string NormalizeRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message, ErrorCodes.AuthenticationRequired);
        }

        return value.Trim();
    }

    private static string NormalizeContent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("消息内容不能为空", ErrorCodes.ParameterInvalid);
        }

        var content = value.Trim();
        if (content.Length > 4000)
        {
            throw new ValidationException("消息内容不能超过 4000 字", ErrorCodes.ParameterInvalid);
        }

        return content;
    }

    private static string NormalizeMessageType(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Text" : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildPreview(string content) =>
        content.Length <= 80 ? content : $"{content[..80]}...";
}

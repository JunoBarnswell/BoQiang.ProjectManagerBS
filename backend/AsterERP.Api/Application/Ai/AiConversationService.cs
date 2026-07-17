using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiConversationService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    AiConversationCompressor compressor) : IAiConversationService
{
    public async Task<GridPageResult<AiConversationDto>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = db.Queryable<AiConversationEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.Title.Contains(keyword) || (item.Summary != null && item.Summary.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.LastMessageAt, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total);
        return new GridPageResult<AiConversationDto> { Total = total.Value, Items = rows.Select(MapConversation).ToList() };
    }

    public async Task<AiConversationDetailDto> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        var conversation = await RequireConversationAsync(id, cancellationToken);
        var messages = await db.Queryable<AiMessageEntity>()
            .Where(item => !item.IsDeleted && item.ConversationId == id)
            .OrderBy(item => item.Seq)
            .ToListAsync(cancellationToken);
        var snapshots = await GetSnapshotsAsync(id, cancellationToken);
        var detail = new AiConversationDetailDto
        {
            Messages = messages.Select(MapMessage).ToList(),
            Snapshots = snapshots
        };
        CopyConversation(conversation, detail);
        return detail;
    }

    public async Task<AiConversationDto> CreateAsync(AiConversationCreateRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var title = string.IsNullOrWhiteSpace(request.Title) ? "新会话" : request.Title.Trim();
        var entity = new AiConversationEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            Title = title,
            Status = "Active",
            LastMessageAt = DateTime.UtcNow
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapConversation(entity);
    }

    public async Task<AiConversationDto> UpdateAsync(string id, AiConversationUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationException("会话标题不能为空", ErrorCodes.ParameterInvalid);
        }

        var entity = await RequireConversationAsync(id, cancellationToken);
        entity.Title = request.Title.Trim();
        entity.IsFavorite = request.IsFavorite;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return MapConversation(entity);
    }

    public async Task<AiConversationDto> UpdateStatusAsync(string id, AiConversationStatusRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Status.Trim();
        if (status is not ("Active" or "Archived"))
        {
            throw new ValidationException("会话状态无效", ErrorCodes.ParameterInvalid);
        }

        var entity = await RequireConversationAsync(id, cancellationToken);
        entity.Status = status;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return MapConversation(entity);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireConversationAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<GridPageResult<AiMessageDto>> GetMessagesAsync(string conversationId, GridQuery query, CancellationToken cancellationToken = default)
    {
        _ = await RequireConversationAsync(conversationId, cancellationToken);
        var dbQuery = db.Queryable<AiMessageEntity>()
            .Where(item => !item.IsDeleted && item.ConversationId == conversationId);
        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.Seq)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total);
        return new GridPageResult<AiMessageDto> { Total = total.Value, Items = rows.Select(MapMessage).ToList() };
    }

    public async Task<IReadOnlyList<AiContextSnapshotDto>> GetSnapshotsAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        _ = await RequireConversationAsync(conversationId, cancellationToken);
        var rows = await db.Queryable<AiContextSnapshotEntity>()
            .Where(item => !item.IsDeleted && item.ConversationId == conversationId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return rows.Select(MapSnapshot).ToList();
    }

    public async Task<AiContextSnapshotDto> CompressAsync(string conversationId, string? modelConfigId, CancellationToken cancellationToken = default)
    {
        var conversation = await RequireConversationAsync(conversationId, cancellationToken);
        var snapshot = await compressor.CompressAsync(conversation, modelConfigId, cancellationToken);
        return MapSnapshot(snapshot);
    }

    public async Task<bool> FeedbackAsync(string messageId, AiMessageFeedbackRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Rating is not ("up" or "down"))
        {
            throw new ValidationException("反馈类型无效", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var message = await db.Queryable<AiMessageEntity>()
            .FirstAsync(item => item.Id == messageId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("消息不存在", ErrorCodes.AiMessageNotFound);

        var feedback = new AiFeedbackEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ConversationId = message.ConversationId,
            MessageId = message.Id,
            RunId = message.RunId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim()
        };
        await db.Insertable(feedback).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public async Task<AiConversationEntity> RequireConversationAsync(string id, CancellationToken cancellationToken) =>
        await db.Queryable<AiConversationEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("会话不存在", ErrorCodes.AiConversationNotFound);

    public static AiConversationDto MapConversation(AiConversationEntity entity)
    {
        var dto = new AiConversationDto();
        CopyConversation(entity, dto);
        return dto;
    }

    public static AiMessageDto MapMessage(AiMessageEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        RunId = entity.RunId,
        AgentProfileId = entity.AgentProfileId,
        Role = entity.Role,
        Seq = entity.Seq,
        Content = entity.Content,
        ReasoningContent = entity.ReasoningContent,
        MetadataJson = entity.MetadataJson,
        Status = entity.Status,
        FinishReason = entity.FinishReason,
        TokenCount = entity.TokenCount,
        CreatedTime = entity.CreatedTime
    };

    private static AiContextSnapshotDto MapSnapshot(AiContextSnapshotEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        FromSeq = entity.FromSeq,
        ToSeq = entity.ToSeq,
        Summary = entity.Summary,
        TotalTokens = entity.TotalTokens,
        CreatedTime = entity.CreatedTime
    };

    private static void CopyConversation(AiConversationEntity entity, AiConversationDto dto)
    {
        dto.Id = entity.Id;
        dto.TenantId = entity.TenantId;
        dto.AppCode = entity.AppCode;
        dto.OwnerUserId = entity.OwnerUserId;
        dto.Title = entity.Title;
        dto.Status = entity.Status;
        dto.IsFavorite = entity.IsFavorite;
        dto.Summary = entity.Summary;
        dto.LastRunStatus = entity.LastRunStatus;
        dto.LastMessageAt = entity.LastMessageAt;
        dto.CreatedTime = entity.CreatedTime;
        dto.UpdatedTime = entity.UpdatedTime;
    }
}

using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiConversationCompressor(
    ISqlSugarClient db,
    IAiModelRouter modelRouter,
    AiKernelChatRuntime chatRuntime,
    AiRunConcurrencyGuard concurrencyGuard)
{
    public async Task<AiContextSnapshotEntity> CompressAsync(
        AiConversationEntity conversation,
        string? modelConfigId,
        CancellationToken cancellationToken)
    {
        await concurrencyGuard.EnsureNoActiveRunAsync(conversation.Id, cancellationToken);
        var messages = await db.Queryable<AiMessageEntity>()
            .Where(item => !item.IsDeleted && item.ConversationId == conversation.Id)
            .OrderBy(item => item.Seq)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            throw new ValidationException("当前会话没有可压缩的消息", ErrorCodes.ParameterInvalid);
        }

        var endpoint = await modelRouter.ResolveAsync(modelConfigId, cancellationToken);
        var source = string.Join("\n", messages.Select(item => $"{item.Role}: {item.Content}"));
        var summary = await chatRuntime.CompleteAsync(new AiKernelChatRequest
        {
            Endpoint = endpoint,
            Messages =
            [
                new ChatMessageContent(AuthorRole.System, "你负责压缩企业 ERP/MES 对话上下文，保留事实、决策、待办、约束和关键引用，输出结构化中文摘要。"),
                new ChatMessageContent(AuthorRole.User, source)
            ]
        }, cancellationToken);

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new BusinessException(ErrorCodes.AiStreamInterrupted, "模型未返回有效摘要");
        }

        var snapshot = new AiContextSnapshotEntity
        {
            TenantId = conversation.TenantId,
            AppCode = conversation.AppCode,
            OwnerUserId = conversation.OwnerUserId,
            ConversationId = conversation.Id,
            FromSeq = messages.Min(item => item.Seq),
            ToSeq = messages.Max(item => item.Seq),
            Summary = summary,
            ModelConfigId = endpoint.ModelConfigId,
            SnapshotType = "Manual"
        };

        await db.Insertable(snapshot).ExecuteCommandAsync(cancellationToken);
        conversation.Summary = summary;
        conversation.ActiveSnapshotId = snapshot.Id;
        conversation.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(conversation).ExecuteCommandAsync(cancellationToken);
        return snapshot;
    }
}

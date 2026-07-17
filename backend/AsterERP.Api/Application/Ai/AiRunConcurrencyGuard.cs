using AsterERP.Api.Modules.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Medallion.Threading;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiRunConcurrencyGuard(ISqlSugarClient db, IDistributedLockProvider lockProvider)
{
    public async Task<IAsyncDisposable> AcquireConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        var handle = await lockProvider.CreateLock($"ai:conversation:{conversationId}")
            .AcquireAsync(TimeSpan.FromSeconds(10), cancellationToken);
        return handle;
    }

    public async Task EnsureNoActiveRunAsync(string conversationId, CancellationToken cancellationToken)
    {
        var hasActiveRun = await db.Queryable<AiChatRunEntity>()
            .AnyAsync(item =>
                !item.IsDeleted &&
                item.ConversationId == conversationId &&
                (item.Status == "Queued" || item.Status == "Running" || item.Status == "Cancelling"),
                cancellationToken);
        if (hasActiveRun)
        {
            throw new ValidationException("当前会话已有运行中的生成任务，请等待完成或停止后重试", ErrorCodes.AiRunConflict);
        }
    }
}

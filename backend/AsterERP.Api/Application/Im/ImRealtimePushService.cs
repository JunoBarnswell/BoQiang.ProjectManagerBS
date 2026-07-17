using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.SignalR;
using AsterERP.Api.Modules.Im;
using AsterERP.Contracts.Im;
using Microsoft.AspNetCore.SignalR;
using SqlSugar;

namespace AsterERP.Api.Application.Im;

public sealed class ImRealtimePushService(
    IHubContext<SystemNotificationHub> hubContext,
    IWorkspaceDatabaseAccessor databaseAccessor) : IImRealtimePushService
{
    public async Task PushMessageAsync(string tenantId, string targetUserId, ImMessageResponse message, CancellationToken cancellationToken = default)
    {
        await LogAsync(tenantId, message.ConversationId, message.Id, targetUserId, "Pending", null, cancellationToken);
        try
        {
            await hubContext.Clients
                .Group(SystemNotificationHub.BuildImUserGroupName(tenantId, targetUserId))
                .SendAsync("ImMessageReceived", message, cancellationToken);
            await LogAsync(tenantId, message.ConversationId, message.Id, targetUserId, "Sent", null, cancellationToken);
        }
        catch (Exception ex)
        {
            await LogAsync(tenantId, message.ConversationId, message.Id, targetUserId, "Failed", ex.Message, cancellationToken);
            throw;
        }
    }

    public Task PushUnreadChangedAsync(string tenantId, string targetUserId, ImUnreadSummaryResponse unread, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(SystemNotificationHub.BuildImUserGroupName(tenantId, targetUserId))
            .SendAsync("ImUnreadChanged", unread, cancellationToken);

    private Task LogAsync(
        string tenantId,
        string conversationId,
        string? messageId,
        string targetUserId,
        string result,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var log = new ImMessageDeliveryLogEntity
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            MessageId = messageId,
            TargetUserId = targetUserId,
            Result = result,
            ErrorMessage = errorMessage
        };
        return databaseAccessor.MainDb.Insertable(log).ExecuteCommandAsync(cancellationToken);
    }
}

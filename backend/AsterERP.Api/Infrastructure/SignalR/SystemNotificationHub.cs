namespace AsterERP.Api.Infrastructure.SignalR;

using AsterERP.Api.Application.Im;
using AsterERP.Api.Infrastructure.Security;
using Microsoft.AspNetCore.SignalR;

public sealed class SystemNotificationHub(IImPresenceService presenceService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst(AsterErpClaimTypes.TenantId)?.Value;
        var appCode = Context.User?.FindFirst(AsterErpClaimTypes.AppCode)?.Value;
        var userId = Context.User?.FindFirst(AsterErpClaimTypes.UserId)?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildImUserGroupName(tenantId, userId));
        }

        if (!string.IsNullOrWhiteSpace(tenantId) &&
            !string.IsNullOrWhiteSpace(appCode) &&
            !string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildImPresenceGroupName(tenantId, appCode));
            var changed = await presenceService.ConnectedAsync(tenantId, appCode, userId, Context.ConnectionAborted);
            if (changed is not null)
            {
                await Clients.Group(BuildImPresenceGroupName(tenantId, appCode))
                    .SendAsync("ImPresenceChanged", changed, Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.User?.FindFirst(AsterErpClaimTypes.TenantId)?.Value;
        var appCode = Context.User?.FindFirst(AsterErpClaimTypes.AppCode)?.Value;
        var userId = Context.User?.FindFirst(AsterErpClaimTypes.UserId)?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId) &&
            !string.IsNullOrWhiteSpace(appCode) &&
            !string.IsNullOrWhiteSpace(userId))
        {
            var changed = await presenceService.DisconnectedAsync(tenantId, appCode, userId);
            if (changed is not null)
            {
                await Clients.Group(BuildImPresenceGroupName(tenantId, appCode))
                    .SendAsync("ImPresenceChanged", changed);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public static string BuildImUserGroupName(string tenantId, string userId) =>
        $"im:user:{tenantId.Trim()}:{userId.Trim()}";

    public static string BuildImPresenceGroupName(string tenantId, string appCode) =>
        $"im:presence:{tenantId.Trim()}:{appCode.Trim().ToUpperInvariant()}";
}

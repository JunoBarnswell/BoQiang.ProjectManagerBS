using AsterERP.Contracts.Im;

namespace AsterERP.Api.Application.Im;

public interface IImRealtimePushService
{
    Task PushMessageAsync(string tenantId, string targetUserId, ImMessageResponse message, CancellationToken cancellationToken = default);

    Task PushUnreadChangedAsync(string tenantId, string targetUserId, ImUnreadSummaryResponse unread, CancellationToken cancellationToken = default);
}

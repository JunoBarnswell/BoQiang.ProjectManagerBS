using AsterERP.Contracts.Im;

namespace AsterERP.Api.Application.Im;

public interface IImPresenceService
{
    Task<ImPresenceChangedResponse?> ConnectedAsync(string tenantId, string appCode, string userId, CancellationToken cancellationToken = default);

    Task<ImPresenceChangedResponse?> DisconnectedAsync(string tenantId, string appCode, string userId, CancellationToken cancellationToken = default);

    IReadOnlySet<string> GetOnlineUserIds(string tenantId, string appCode);
}

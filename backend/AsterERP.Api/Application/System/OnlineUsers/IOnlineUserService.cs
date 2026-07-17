using AsterERP.Shared;
using AsterERP.Contracts.System.OnlineUsers;

namespace AsterERP.Api.Application.System.OnlineUsers;

public interface IOnlineUserService
{
    Task<GridPageResult<OnlineUserResponse>> GetPageAsync(OnlineUserQuery query, CancellationToken cancellationToken = default);

    Task ForceLogoutAsync(string sessionId, CancellationToken cancellationToken = default);
}

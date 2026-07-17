using AsterERP.Shared;
using AsterERP.Contracts.Logs;

namespace AsterERP.Api.Application.System.LoginLogs;

public interface ILoginLogService
{
    Task WriteAsync(LoginLogWriteRequest request, CancellationToken cancellationToken = default);

    Task<GridPageResult<LoginLogListItemResponse>> GetPageAsync(
        LoginLogQuery query,
        CancellationToken cancellationToken = default);
}

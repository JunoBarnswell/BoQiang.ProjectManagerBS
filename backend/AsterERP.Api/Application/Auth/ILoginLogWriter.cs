using AsterERP.Contracts.Logs;

namespace AsterERP.Api.Application.Auth;

public interface ILoginLogWriter
{
    Task WriteAsync(LoginLogWriteRequest request, CancellationToken cancellationToken = default);
}

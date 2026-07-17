using AsterERP.Contracts.Platform;

namespace AsterERP.Api.Application.Platform.Applications;

public interface IPlatformApplicationEntryService
{
    Task<ApplicationBackendEntryResponse> EnterAsync(
        string appCode,
        ApplicationBackendEntryRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default);
}

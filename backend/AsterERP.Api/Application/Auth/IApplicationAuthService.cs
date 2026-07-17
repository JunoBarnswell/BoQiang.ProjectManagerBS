using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Contracts.Auth;

namespace AsterERP.Api.Application.Auth;

public interface IApplicationAuthService
{
    Task<ApplicationLoginBootstrapResponse> GetBootstrapAsync(
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default);

    Task<ApplicationDatabaseBindingResponse> TestInitialDatabaseBindingAsync(
        string tenantId,
        string appCode,
        ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationDatabaseBindingResponse> SaveInitialDatabaseBindingAsync(
        string tenantId,
        string appCode,
        ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationLoginResponse> LoginAsync(
        string tenantId,
        string appCode,
        ApplicationLoginRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default);
}

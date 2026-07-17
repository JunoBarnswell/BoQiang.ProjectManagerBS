using AsterERP.Contracts.Auth;

namespace AsterERP.Api.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, HttpContext httpContext, CancellationToken cancellationToken = default);

    Task RecoverInitialAdminPasswordAsync(
        InitialAdminPasswordRecoveryRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default);

    Task<SessionResponse> GetSessionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceResponse>> GetWorkspacesAsync(CancellationToken cancellationToken = default);

    Task<SwitchWorkspaceResponse> SwitchWorkspaceAsync(SwitchWorkspaceRequest request, HttpContext httpContext, CancellationToken cancellationToken = default);

    Task<SwitchWorkspaceResponse> SwitchPlatformAsync(SwitchPlatformRequest request, HttpContext httpContext, CancellationToken cancellationToken = default);

    Task<CurrentWorkspaceResponse?> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default);
}

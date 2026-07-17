using AsterERP.Api.Modules.System.Users;

namespace AsterERP.Api.Infrastructure.Security;

public interface IAuthSessionService
{
    const string SessionCookieName = "astererp_session";
    const string CsrfCookieName = "astererp_csrf";
    const string CsrfHeaderName = "X-CSRF-Token";

    Task<string> CreateSessionAsync(SystemUserEntity user, HttpContext httpContext, CancellationToken cancellationToken);

    Task<ResolvedAuthenticatedUser> ResolveAsync(string? authorizationHeader, CancellationToken cancellationToken = default);

    Task<ResolvedAuthenticatedUser> ResolveAsync(
        string? authorizationHeader,
        string? sessionCookie,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateCsrfTokenAsync(
        string? sessionCookie,
        string? csrfCookie,
        string? csrfHeader,
        CancellationToken cancellationToken = default)
        => Task.FromResult(
            !string.IsNullOrWhiteSpace(csrfCookie) &&
            !string.IsNullOrWhiteSpace(csrfHeader) &&
            string.Equals(csrfCookie, csrfHeader, StringComparison.Ordinal));

    Task<string> RefreshCurrentSessionAsync(HttpContext httpContext, CancellationToken cancellationToken = default);

    Task RevokeCurrentSessionAsync(HttpContext httpContext, CancellationToken cancellationToken = default);

    Task SetCurrentWorkspaceAsync(
        string? authorizationHeader,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default);

    Task InvalidateSessionCacheAsync(string? authorizationHeader, CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task RevokeSessionsByUserIdsAsync(IReadOnlyList<string> userIds, CancellationToken cancellationToken = default);
}

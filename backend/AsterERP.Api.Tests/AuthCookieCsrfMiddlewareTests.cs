using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AuthCookieCsrfMiddlewareTests
{
    [Fact]
    public async Task Cookie_authenticated_write_without_csrf_is_rejected()
    {
        var context = CreateContext("POST", "/api/system-users", "session-token", null, null);
        var nextCalled = false;
        var middleware = new CurrentUserMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new FixedAuthSessionService());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Cookie_authenticated_write_with_matching_csrf_is_allowed()
    {
        var context = CreateContext("POST", "/api/system-users", "session-token", "csrf-token", "csrf-token");
        var nextCalled = false;
        var middleware = new CurrentUserMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new FixedAuthSessionService());

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Bearer_authenticated_write_remains_compatible_without_csrf()
    {
        var context = CreateContext("POST", "/api/system-users", null, null, null);
        context.Request.Headers.Authorization = "Bearer desktop-token";
        var nextCalled = false;
        var middleware = new CurrentUserMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new FixedAuthSessionService());

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextCalled);
    }

    private static DefaultHttpContext CreateContext(
        string method,
        string path,
        string? session,
        string? csrfCookie,
        string? csrfHeader)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (session is not null)
        {
            context.Request.Headers.Cookie = $"{IAuthSessionService.SessionCookieName}={session}; {IAuthSessionService.CsrfCookieName}={csrfCookie}";
        }
        if (csrfHeader is not null)
        {
            context.Request.Headers[IAuthSessionService.CsrfHeaderName] = csrfHeader;
        }
        return context;
    }

    private sealed class FixedAuthSessionService : IAuthSessionService
    {
        private static readonly ResolvedAuthenticatedUser AuthenticatedUser = new(
            "user-1", "admin", "tenant-a", "Tenant A", "MES", "MES", null, null,
            [], [], [], "ALL", true, false, true);

        public Task<ResolvedAuthenticatedUser> ResolveAsync(string? authorizationHeader, CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthenticatedUser);

        public Task<ResolvedAuthenticatedUser> ResolveAsync(string? authorizationHeader, string? sessionCookie, CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthenticatedUser);

        public Task<string> CreateSessionAsync(AsterERP.Api.Modules.System.Users.SystemUserEntity user, HttpContext httpContext, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> RefreshCurrentSessionAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RevokeCurrentSessionAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetCurrentWorkspaceAsync(string? authorizationHeader, string tenantId, string appCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task InvalidateSessionCacheAsync(string? authorizationHeader, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RevokeSessionsByUserIdsAsync(IReadOnlyList<string> userIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

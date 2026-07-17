using AsterERP.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AuthSessionCookieWriterTests
{
    [Fact]
    public void Session_cookie_is_secure_http_only_and_lax()
    {
        var context = new DefaultHttpContext();

        new AuthSessionCookieWriter().Write(context, "session-token", "csrf-token");

        var headers = context.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{IAuthSessionService.SessionCookieName}=session-token", headers, StringComparison.Ordinal);
        Assert.Contains("secure", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{IAuthSessionService.CsrfCookieName}=csrf-token", headers, StringComparison.Ordinal);
    }

    [Fact]
    public void Rotation_replaces_both_session_and_csrf_cookie_values()
    {
        var context = new DefaultHttpContext();
        var writer = new AuthSessionCookieWriter();

        writer.Write(context, "first-session", "first-csrf");
        writer.Write(context, "second-session", "second-csrf");

        var headers = context.Response.Headers.SetCookie.ToArray();
        Assert.Equal(4, headers.Length);
        Assert.Contains("second-session", headers[2], StringComparison.Ordinal);
        Assert.Contains("second-csrf", headers[3], StringComparison.Ordinal);
    }
}

namespace AsterERP.Api.Infrastructure.Security;

public sealed class AuthSessionCookieWriter
{
    public void Write(HttpContext httpContext, string sessionToken, string csrfToken)
    {
        httpContext.Response.Cookies.Append(
            IAuthSessionService.SessionCookieName,
            sessionToken,
            new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, IsEssential = true, Path = "/" });
        httpContext.Response.Cookies.Append(
            IAuthSessionService.CsrfCookieName,
            csrfToken,
            new CookieOptions { HttpOnly = false, Secure = true, SameSite = SameSiteMode.Lax, IsEssential = true, Path = "/" });
    }

    public void Clear(HttpContext httpContext)
    {
        var options = new CookieOptions { Secure = true, SameSite = SameSiteMode.Lax, Path = "/" };
        httpContext.Response.Cookies.Delete(IAuthSessionService.SessionCookieName, options);
        httpContext.Response.Cookies.Delete(IAuthSessionService.CsrfCookieName, options);
    }
}

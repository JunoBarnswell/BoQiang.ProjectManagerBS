using System.Security.Cryptography;
using AsterERP.Shared;

namespace AsterERP.Api.Infrastructure.Security;

public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuthSessionService authSessionService)
    {
        var resolvedUser = await authSessionService.ResolveAsync(
            ResolveAuthorizationHeader(context),
            context.Request.Cookies[IAuthSessionService.SessionCookieName],
            context.RequestAborted);

        if (RequiresCsrfValidation(context, resolvedUser) &&
            !await authSessionService.ValidateCsrfTokenAsync(
                context.Request.Cookies[IAuthSessionService.SessionCookieName],
                context.Request.Cookies[IAuthSessionService.CsrfCookieName],
                context.Request.Headers[IAuthSessionService.CsrfHeaderName].ToString(),
                context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                ApiResultFactory.Fail<object?>(
                    "CSRF 校验失败",
                    context.TraceIdentifier,
                    ErrorCodes.PermissionDenied),
                context.RequestAborted);
            return;
        }

        if (!IsWorkspaceHeaderConsistent(context, resolvedUser))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                ApiResultFactory.Fail<object?>(
                    "请求工作区上下文与当前会话不一致",
                    context.TraceIdentifier,
                    ErrorCodes.PermissionDenied),
                context.RequestAborted);
            return;
        }

        context.User = AsterErpClaimsPrincipalFactory.Create(resolvedUser);
        await next(context);
    }

    private static bool IsWorkspaceHeaderConsistent(HttpContext context, ResolvedAuthenticatedUser user)
    {
        var tenantHeader = context.Request.Headers["X-Tenant-Id"].ToString();
        var appHeader = context.Request.Headers["X-App-Code"].ToString();
        var workspaceLevelHeader = context.Request.Headers["X-Workspace-Level"].ToString();

        if (string.IsNullOrWhiteSpace(tenantHeader) &&
            string.IsNullOrWhiteSpace(appHeader) &&
            string.IsNullOrWhiteSpace(workspaceLevelHeader))
        {
            return true;
        }

        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.TenantId) || string.IsNullOrWhiteSpace(user.AppCode))
        {
            return false;
        }

        var derivedWorkspaceLevel = string.Equals(user.AppCode, "SYSTEM", StringComparison.OrdinalIgnoreCase)
            ? "platform"
            : "application";

        return IsHeaderMissingOrEqual(tenantHeader, user.TenantId) &&
               IsHeaderMissingOrEqual(appHeader, user.AppCode) &&
               IsHeaderMissingOrEqual(workspaceLevelHeader, derivedWorkspaceLevel);
    }

    private static bool IsHeaderMissingOrEqual(string headerValue, string? expectedValue) =>
        string.IsNullOrWhiteSpace(headerValue) ||
        string.Equals(headerValue, expectedValue, StringComparison.OrdinalIgnoreCase);

    private static string ResolveAuthorizationHeader(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            return authorization;
        }

        if (!context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var accessToken = context.Request.Query["access_token"].ToString();
        return string.IsNullOrWhiteSpace(accessToken) ? string.Empty : $"Bearer {accessToken}";
    }

    private static bool RequiresCsrfValidation(HttpContext context, ResolvedAuthenticatedUser user)
    {
        if (!user.IsAuthenticated ||
            !string.IsNullOrWhiteSpace(context.Request.Headers.Authorization) ||
            !HttpMethods.IsPost(context.Request.Method) &&
            !HttpMethods.IsPut(context.Request.Method) &&
            !HttpMethods.IsPatch(context.Request.Method) &&
            !HttpMethods.IsDelete(context.Request.Method))
        {
            return false;
        }

        return !IsAuthenticationEntryPoint(context.Request.Path);
    }

    private static bool IsAuthenticationEntryPoint(PathString path) =>
        path.Equals("/api/auth/login") ||
        path.StartsWithSegments("/api/application-auth/tenants") &&
        path.Value?.EndsWith("/login", StringComparison.OrdinalIgnoreCase) == true;

}

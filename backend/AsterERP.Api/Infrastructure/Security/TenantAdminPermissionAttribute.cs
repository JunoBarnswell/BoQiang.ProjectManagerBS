using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Volo.Abp.Users;

namespace AsterERP.Api.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TenantAdminPermissionAttribute(string code) : Attribute, IAsyncAuthorizationFilter
{
    public string Code { get; } = code;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<TenantAdminPermissionAttribute>();

        if (!currentUser.IsAsterErpAuthenticated())
        {
            context.Result = new JsonResult(ApiResultFactory.Fail<object?>(
                "请先登录",
                context.HttpContext.TraceIdentifier,
                ErrorCodes.AuthenticationRequired))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return Task.CompletedTask;
        }

        var isTenantWorkspace = !string.IsNullOrWhiteSpace(currentUser.GetAsterErpTenantId());
        var canManageTenant = isTenantWorkspace &&
            (currentUser.IsAsterErpTenantAdmin() || currentUser.HasAsterErpPermission("*"));
        if (!canManageTenant)
        {
            logger.LogWarning(
                "Tenant admin permission denied for user {UserName} on {Path} with code {PermissionCode}",
                currentUser.UserName,
                context.HttpContext.Request.Path,
                Code);

            context.Result = new JsonResult(ApiResultFactory.Fail<object?>(
                "仅租户超级管理员可访问该接口",
                context.HttpContext.TraceIdentifier,
                ErrorCodes.PermissionDenied))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return Task.CompletedTask;
    }
}

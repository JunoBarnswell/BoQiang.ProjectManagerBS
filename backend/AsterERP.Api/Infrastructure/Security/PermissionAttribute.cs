using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Volo.Abp.Users;

namespace AsterERP.Api.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class PermissionAttribute(string code) : Attribute, IAsyncAuthorizationFilter
{
    public string Code { get; } = code;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<PermissionAttribute>();

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

        if (!currentUser.HasAsterErpPermission(Code))
        {
            logger.LogWarning(
                "Permission denied for user {UserName} on {Path} with code {PermissionCode}",
                currentUser.UserName,
                context.HttpContext.Request.Path,
                Code);

            context.Result = new JsonResult(ApiResultFactory.Fail<object?>(
                "无权限访问该接口",
                context.HttpContext.TraceIdentifier,
                ErrorCodes.PermissionDenied))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return Task.CompletedTask;
    }
}

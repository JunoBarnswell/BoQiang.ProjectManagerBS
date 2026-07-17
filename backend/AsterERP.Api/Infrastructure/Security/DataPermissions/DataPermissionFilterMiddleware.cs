namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class DataPermissionFilterMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IDataPermissionFilterRegistrar filterRegistrar)
    {
        using var filterScope = await filterRegistrar.RegisterAsync(context.RequestAborted);
        await next(context);
    }
}

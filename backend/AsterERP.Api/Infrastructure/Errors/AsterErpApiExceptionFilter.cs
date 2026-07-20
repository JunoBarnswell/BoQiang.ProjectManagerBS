using System.Diagnostics;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Infrastructure.Errors;

public sealed class AsterErpApiExceptionFilter(
    ILogger<AsterErpApiExceptionFilter> logger,
    IConfiguration configuration) : IAsyncActionFilter, IExceptionFilter, IOrderedFilter
{
    public int Order => int.MinValue;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var executedContext = await next();
            if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
            {
                executedContext.ExceptionHandled = TryHandleBusinessException(
                    executedContext.HttpContext,
                    executedContext.Exception,
                    out var result);
                if (executedContext.ExceptionHandled)
                {
                    executedContext.Result = result;
                }
            }

            if (AuthenticationLoginFailureResponsePolicy.ShouldNormalize(
                    executedContext.HttpContext.Request.Path,
                    executedContext.Result))
            {
                executedContext.Result = AuthenticationLoginFailureResponsePolicy.CreateGenericResult(
                    executedContext.HttpContext);
                await AuthenticationLoginFailureResponsePolicy.EnforceMinimumDelayAsync(
                    startedTimestamp,
                    AuthenticationLoginFailureResponsePolicy.ResolveMinimumDelay(configuration));
            }
        }
        catch (Exception exception)
        {
            if (TryHandleBusinessException(context.HttpContext, exception, out var result))
            {
                context.Result = result;
                if (AuthenticationLoginFailureResponsePolicy.ShouldNormalize(
                        context.HttpContext.Request.Path,
                        context.Result))
                {
                    context.Result = AuthenticationLoginFailureResponsePolicy.CreateGenericResult(
                        context.HttpContext);
                    await AuthenticationLoginFailureResponsePolicy.EnforceMinimumDelayAsync(
                        startedTimestamp,
                        AuthenticationLoginFailureResponsePolicy.ResolveMinimumDelay(configuration));
                }

                return;
            }

            throw;
        }
    }

    public void OnException(ExceptionContext context)
    {
        if (!TryHandleBusinessException(context.HttpContext, context.Exception, out var result))
        {
            return;
        }

        context.Result = AuthenticationLoginFailureResponsePolicy.ShouldNormalize(
                context.HttpContext.Request.Path,
                result)
            ? AuthenticationLoginFailureResponsePolicy.CreateGenericResult(context.HttpContext)
            : result;
        context.ExceptionHandled = true;
    }

    private bool TryHandleBusinessException(HttpContext httpContext, Exception exception, out IActionResult result)
    {
        var mapped = AsterErpExceptionStatusMapper.Map(exception);
        if (mapped.StatusCode == StatusCodes.Status500InternalServerError)
        {
            result = new EmptyResult();
            return false;
        }

        logger.LogWarning(
            exception,
            "Handled API business exception {Code} on {Path}",
            mapped.Code,
            httpContext.Request.Path);

        result = new ObjectResult(ApiResultFactory.Fail<object?>(
            mapped.Message,
            httpContext.TraceIdentifier,
            mapped.Code,
            mapped.MessageKey,
            mapped.MessageArguments))
        {
            StatusCode = mapped.StatusCode
        };
        return true;
    }
}

internal static class AuthenticationLoginFailureResponsePolicy
{
    public const string GenericMessage = "账号或密码错误";

    internal static bool ShouldNormalize(PathString path, IActionResult? result) =>
        IsLoginPath(path) && ResolveStatusCode(result) == StatusCodes.Status401Unauthorized;

    internal static bool IsLoginPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return string.Equals(value, "/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/api/application-auth/tenants", StringComparison.OrdinalIgnoreCase) &&
               value.EndsWith("/login", StringComparison.OrdinalIgnoreCase);
    }

    internal static TimeSpan ResolveMinimumDelay(IConfiguration configuration)
    {
        var milliseconds = Math.Clamp(
            configuration.GetValue("Auth:LoginFailureMinimumMilliseconds", 350),
            100,
            2000);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    internal static IActionResult CreateGenericResult(HttpContext httpContext) =>
        new ObjectResult(ApiResultFactory.Fail<object?>(
            GenericMessage,
            httpContext.TraceIdentifier,
            ErrorCodes.AuthenticationRequired))
        {
            StatusCode = StatusCodes.Status401Unauthorized
        };

    internal static async Task EnforceMinimumDelayAsync(
        long startedTimestamp,
        TimeSpan minimumDelay)
    {
        var remaining = minimumDelay - Stopwatch.GetElapsedTime(startedTimestamp);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, CancellationToken.None);
        }
    }

    private static int? ResolveStatusCode(IActionResult? result) => result switch
    {
        ObjectResult objectResult => objectResult.StatusCode,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        _ => null
    };
}

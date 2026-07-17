using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Abp.Settings;
using AsterERP.Api.Modules.System.Logs;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Diagnostics;
using Volo.Abp.Settings;

namespace AsterERP.Api.Infrastructure.Logging;

public sealed class OperationLogMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IOperationLogQueue operationLogQueue,
        ICurrentUser currentUser,
        ISettingProvider settingProvider)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!await IsEnabledAsync(settingProvider, AsterErpSettingNames.AuditOperationLogEnabled, true))
        {
            await next(context);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        Exception? exception = null;

        try
        {
            await next(context);
        }
        catch (Exception caughtException)
        {
            exception = caughtException;
            throw;
        }
        finally
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            var handledException = exceptionFeature?.Error;
            var requestPath = exceptionFeature?.Path ?? context.Request.Path.Value ?? string.Empty;
            var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var statusCode = context.Response.StatusCode;
            var captureQueryString = await IsEnabledAsync(settingProvider, AsterErpSettingNames.AuditCaptureQueryString, true);
            var operationLog = new SystemOperationLogEntity
            {
                TraceId = context.TraceIdentifier,
                CorrelationId = Activity.Current?.Id ?? context.TraceIdentifier,
                RequestPath = requestPath,
                RequestMethod = context.Request.Method,
                RouteDisplayName = exceptionFeature is null ? context.GetEndpoint()?.DisplayName : requestPath,
                ModuleName = requestPath.StartsWith("/api/system", StringComparison.OrdinalIgnoreCase)
                    ? "System"
                    : "General",
                OperationType = context.Request.Method,
                ActionName = exceptionFeature is null ? ResolveActionName(context) : requestPath,
                RequestQuery = captureQueryString && context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                UserId = currentUser.GetAsterErpUserId(),
                UserName = currentUser.UserName,
                ErrorMessage = exception?.Message ?? handledException?.Message,
                ExceptionSummary = SummarizeException(exception ?? handledException),
                StatusCode = statusCode,
                DurationMs = (long)Math.Round(durationMs),
                IsSuccess = statusCode is >= 200 and < 400 && exception is null,
                CreatedBy = currentUser.GetAsterErpUserId(),
                CreatedTime = DateTime.UtcNow
            };

            operationLogQueue.TryEnqueue(operationLog);
        }
    }

    private static string? ResolveActionName(HttpContext context)
    {
        var actionDescriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        return actionDescriptor is null
            ? context.GetEndpoint()?.DisplayName
            : $"{actionDescriptor.ControllerName}.{actionDescriptor.ActionName}";
    }

    private static string? SummarizeException(Exception? exception)
    {
        if (exception is null)
        {
            return null;
        }

        var summary = $"{exception.GetType().Name}: {exception.Message}";
        return summary.Length <= 512 ? summary : summary[..512];
    }

    private static async Task<bool> IsEnabledAsync(ISettingProvider settingProvider, string settingName, bool defaultValue)
    {
        var rawValue = await settingProvider.GetOrNullAsync(settingName);
        return bool.TryParse(rawValue, out var enabled) ? enabled : defaultValue;
    }
}

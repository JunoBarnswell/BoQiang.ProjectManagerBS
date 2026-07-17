using System.Diagnostics;

namespace AsterERP.Api.Infrastructure.Diagnostics;

public sealed class RequestDiagnosticsMiddleware(
    RequestDelegate next,
    ILogger<RequestDiagnosticsMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.TraceIdentifier;
        context.Response.Headers["X-Trace-Id"] = traceId;

        var sw = Stopwatch.StartNew();
        logger.LogInformation(
            "HTTP {Method} {Path} started traceId={TraceId}",
            context.Request.Method,
            context.Request.Path,
            traceId);

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            logger.LogInformation(
                "HTTP {Method} {Path} finished {StatusCode} elapsed={ElapsedMilliseconds}ms traceId={TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                traceId);
        }
    }
}

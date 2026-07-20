using AsterERP.Shared;
using Microsoft.AspNetCore.Diagnostics;

namespace AsterERP.Api.Infrastructure.Errors;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
{
    public IResult Handle(HttpContext context)
    {
        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = feature?.Error;

        if (exception is not null)
        {
            logger.LogError(exception, "Unhandled exception while processing {Path}", context.Request.Path);
        }

        var traceId = context.TraceIdentifier;
        var mapped = AsterErpExceptionStatusMapper.Map(exception);

        return Results.Json(ApiResultFactory.Fail<object?>(mapped.Message, traceId, mapped.Code, mapped.MessageKey, mapped.MessageArguments), statusCode: mapped.StatusCode);
    }
}

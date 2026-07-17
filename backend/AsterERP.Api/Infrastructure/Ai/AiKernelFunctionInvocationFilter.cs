using Microsoft.SemanticKernel;

namespace AsterERP.Api.Infrastructure.Ai;

public sealed class AiKernelFunctionInvocationFilter(ILogger<AiKernelFunctionInvocationFilter> logger) : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await next(context);
            logger.LogInformation(
                "SK function invoked. plugin={PluginName}, function={FunctionName}, latencyMs={LatencyMs}",
                context.Function.PluginName,
                context.Function.Name,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "SK function invocation failed. plugin={PluginName}, function={FunctionName}, latencyMs={LatencyMs}",
                context.Function.PluginName,
                context.Function.Name,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

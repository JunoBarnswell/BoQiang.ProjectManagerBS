using Microsoft.SemanticKernel;

namespace AsterERP.Api.Infrastructure.Ai;

public sealed class AiKernelAutoFunctionInvocationFilter(ILogger<AiKernelAutoFunctionInvocationFilter> logger) : IAutoFunctionInvocationFilter
{
    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        logger.LogInformation(
            "SK auto function invocation requested. plugin={PluginName}, function={FunctionName}, requestIndex={RequestIndex}, functionIndex={FunctionIndex}",
            context.Function.PluginName,
            context.Function.Name,
            context.RequestSequenceIndex,
            context.FunctionSequenceIndex);
        await next(context);
    }
}

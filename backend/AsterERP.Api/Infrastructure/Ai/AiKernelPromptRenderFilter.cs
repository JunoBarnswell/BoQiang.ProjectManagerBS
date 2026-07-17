using Microsoft.SemanticKernel;

namespace AsterERP.Api.Infrastructure.Ai;

public sealed class AiKernelPromptRenderFilter(ILogger<AiKernelPromptRenderFilter> logger) : IPromptRenderFilter
{
    public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
    {
        await next(context);
        logger.LogInformation(
            "SK prompt rendered. plugin={PluginName}, function={FunctionName}, renderedLength={RenderedLength}",
            context.Function.PluginName,
            context.Function.Name,
            context.RenderedPrompt?.Length ?? 0);
    }
}

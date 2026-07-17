using AsterERP.Workflow.Core.Context;

namespace AsterERP.Api.Infrastructure.Workflows;

public sealed class WorkflowWorkspaceRuntimeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        WorkflowWorkspaceRuntimeInitializer runtimeInitializer)
    {
        if (context.Request.Path.StartsWithSegments("/api/workflows", StringComparison.OrdinalIgnoreCase))
        {
            using var workflowServiceProviderScope = ProcessEngineServiceProviderAccessor.Push(context.RequestServices);
            await runtimeInitializer.EnsureInitializedAsync(context.RequestAborted);
            await next(context);
            return;
        }

        await next(context);
    }
}

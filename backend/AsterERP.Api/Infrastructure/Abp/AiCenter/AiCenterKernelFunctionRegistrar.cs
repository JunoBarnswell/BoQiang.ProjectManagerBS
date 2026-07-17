using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Application.Ai.Tools.Workflow;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterKernelFunctionRegistrar
{
    public static void Register(IServiceCollection services)
    {
        services.AddScoped<AiKernelFunctionCatalog>();
        services.AddScoped<AiKernelFunctionService>();
        services.AddScoped<AiKernelFunctionArgumentNormalizer>();
        services.AddScoped<AiKernelFunctionPermissionFilter>();
        services.AddScoped<AiKernelFunctionArgumentRedactor>();
        services.AddScoped<AiWorkflowArtifactService>();
        services.AddScoped<WorkflowDraftParser>();
        services.AddScoped<WorkflowBpmnDraftMapper>();
        services.AddScoped<WorkflowBusinessCanvasDraftMapper>();
        services.AddScoped<WorkflowConditionEvaluator>();
        services.AddScoped<WorkflowDraftValidator>();
        services.AddScoped<WorkflowSimulationEngine>();
    }
}

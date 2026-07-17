using AsterERP.Workflow.Processing.Analysis;
using AsterERP.Workflow.Processing.Planning;
using AsterERP.Workflow.Processing.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.Processing;

public static class WorkflowProcessingServiceCollectionExtensions
{
    public static IServiceCollection AddAsterERPWorkflowProcessing(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowProcessingGraphAnalyzer, WorkflowProcessingGraphAnalyzer>();
        services.AddSingleton<IWorkflowProcessingExecutionPlanner, WorkflowProcessingExecutionPlanner>();
        services.AddSingleton<IWorkflowProcessingDefinitionValidator, WorkflowProcessingDefinitionValidator>();
        return services;
    }
}

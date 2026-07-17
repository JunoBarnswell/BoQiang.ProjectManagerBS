using AsterERP.Api.Application.Ai;
using AsterERP.Api.Application.Ai.Agent;
using AsterERP.Api.Application.Ai.KnowledgeGraph;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterApplicationServiceRegistrar
{
    public static void Register(IServiceCollection services)
    {
        services.AddScoped<AiWorkspaceContext>();
        services.AddScoped<AiConversationService>();
        services.AddScoped<IAiConversationService>(serviceProvider => serviceProvider.GetRequiredService<AiConversationService>());
        services.AddScoped<IAiModelConfigurationService, AiModelConfigurationService>();
        services.AddScoped<IAiPromptTemplateService, AiPromptTemplateService>();
        services.AddScoped<IAiAgentProfileService, AiAgentProfileService>();
        services.AddScoped<AiGovernanceService>();
        services.AddScoped<AiWorkbenchService>();
        services.AddScoped<AiObservabilityService>();
        services.AddScoped<AiSettingsService>();
        services.AddScoped<AiToolManagementService>();
        services.AddScoped<AiDataCenterAssistantService>();
        services.AddScoped<IAiTaskPlanService, AiTaskPlanService>();
        services.AddScoped<AiTaskPlanValidator>();
        services.AddScoped<AiTaskPlanGuard>();
        services.AddScoped<AiTaskPlanEventWriter>();
        services.AddScoped<AiPlanParser>();
        services.AddScoped<AiPlanGenerationService>();
        services.AddScoped<IAiAgentExecutionService, AiAgentExecutionService>();
        services.AddScoped<AiWorkflowDraftAutoImportService>();
        services.AddScoped<IAiStreamService, AiStreamService>();
        services.AddScoped<AiContextBuilder>();
        services.AddScoped<AiConversationCompressor>();
        services.AddScoped<AiRunConcurrencyGuard>();
        services.AddScoped<AiSkCapabilityService>();
        services.AddScoped<AiKnowledgeService>();
        services.AddScoped<IAiKnowledgeGraphService, AiKnowledgeGraphService>();
        services.AddScoped<IAiKnowledgeGraphBuildService, AiKnowledgeGraphBuildService>();
        services.AddScoped<IAiKnowledgeGraphImportExportService, AiKnowledgeGraphImportExportService>();
    }
}

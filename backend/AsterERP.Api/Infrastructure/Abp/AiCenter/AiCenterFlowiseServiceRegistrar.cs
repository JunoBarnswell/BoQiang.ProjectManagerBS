using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Api.Infrastructure.Ai;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterFlowiseServiceRegistrar
{
    public static void Register(IServiceCollection services)
    {
        services.AddHttpClient(FlowiseOutboundHttpClient.Name, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(FlowiseOutboundHttpClient.CreatePrimaryHandler);
        services.AddScoped<FlowiseScopedHttpClientFactory>();
        services.AddScoped<FlowisePermissionGuard>();
        services.AddScoped<IFlowiseResourceAccessGuard, FlowiseResourceAccessGuard>();
        services.AddScoped<IFlowiseManagementService, FlowiseManagementService>();
        services.AddScoped<IFlowiseChatflowService, FlowiseChatflowService>();
        services.AddScoped<IFlowiseMcpServerService, FlowiseMcpServerService>();
        services.AddScoped<IFlowiseMcpEndpointService, FlowiseMcpEndpointService>();
        services.AddScoped<IFlowiseCanvasService, FlowiseCanvasService>();
        services.AddScoped<FlowiseRuntimeNodeClassifier>();
        services.AddScoped<FlowiseRuntimeNodeDataReader>();
        services.AddScoped<FlowiseExecutionContentParser>();
        services.AddScoped<FlowiseExecutionJsonDocumentParser>();
        services.AddScoped<FlowiseDocumentStoreReferenceParser>();
        services.AddScoped<FlowiseAgentToolCallParser>();
        services.AddScoped<FlowiseNodeMessageBuilder>();
        services.AddScoped<FlowiseStructuredOutputBuilder>();
        services.AddScoped<FlowiseRuntimeFlowDataParser>();
        services.AddScoped<FlowiseAgentFlowEventBuilder>();
        services.AddScoped<FlowiseExecutionSnapshotBuilder>();
        services.AddScoped<FlowiseVariableResolver>();
        services.AddScoped<FlowiseOutputReferenceResolver>();
        services.AddScoped<FlowiseExecutionTemplateResolver>();
        services.AddScoped<FlowiseKeyValueInputReader>();
        services.AddScoped<FlowiseRuntimeNodeInputResolver>();
        services.AddScoped<FlowiseStateUpdateApplier>();
        services.AddScoped<FlowiseConditionEvaluator>();
        services.AddScoped<FlowiseExecutionOrderPlanner>();
        services.AddScoped<FlowiseExecutionResultBuilder>();
        services.AddScoped<IFlowiseExecutionTrackingService, FlowiseExecutionTrackingService>();
        services.AddScoped<IFlowiseExecutionService>(serviceProvider =>
            ActivatorUtilities.CreateInstance<FlowiseExecutionService>(
                serviceProvider,
                (IHttpClientFactory)serviceProvider.GetRequiredService<FlowiseScopedHttpClientFactory>()));
        services.AddScoped<FlowiseScheduleExecutionRunner>();
        services.AddTransient<FlowiseScheduleExecutionJob>();
        services.AddScoped<IFlowiseNodeCatalogService, FlowiseNodeCatalogService>();
        services.AddScoped<IFlowisePredictionService, FlowisePredictionService>();
        services.AddScoped<IFlowiseWebhookListenerService, FlowiseWebhookListenerService>();
        services.AddScoped<IFlowiseDocumentStoreService, FlowiseDocumentStoreService>();
        services.AddScoped<IFlowiseEvaluationService, FlowiseEvaluationService>();
        services.AddScoped<IFlowiseToolService, FlowiseToolService>();
        services.AddScoped<IFlowiseCustomMcpServerService>(serviceProvider =>
            ActivatorUtilities.CreateInstance<FlowiseCustomMcpServerService>(
                serviceProvider,
                (IHttpClientFactory)serviceProvider.GetRequiredService<FlowiseScopedHttpClientFactory>()));
        services.AddScoped<IFlowiseCredentialService, FlowiseCredentialService>();
        services.AddScoped<IFlowiseVariableService, FlowiseVariableService>();
        services.AddScoped<IFlowiseApiKeyService, FlowiseApiKeyService>();
        services.AddScoped<IFlowiseAssistantService, FlowiseAssistantService>();
        services.AddScoped<IFlowiseMarketplaceService, FlowiseMarketplaceService>();
        services.AddScoped<FlowiseFlowDataValidator>();
    }
}

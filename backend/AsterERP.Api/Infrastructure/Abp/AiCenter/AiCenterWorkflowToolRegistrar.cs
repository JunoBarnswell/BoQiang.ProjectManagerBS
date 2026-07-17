using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Application.Ai.Tools.Workflow;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterWorkflowToolRegistrar
{
    public static void Register(IServiceCollection services)
    {
        services.AddScoped<IAiKernelFunction, WorkflowModelSearchTool>();
        services.AddScoped<IAiKernelFunction, WorkflowModelGetTool>();
        services.AddScoped<IAiKernelFunction, WorkflowModelExplainTool>();
        services.AddScoped<IAiKernelFunction, WorkflowModelCompareVersionsTool>();
        services.AddScoped<IAiKernelFunction, WorkflowBindingSearchTool>();
        services.AddScoped<IAiKernelFunction, WorkflowNotificationRulesTool>();
        services.AddScoped<IAiKernelFunction, WorkflowTaskSearchTool>();
        services.AddScoped<IAiKernelFunction, WorkflowInstanceGetTool>();
        services.AddScoped<IAiKernelFunction, WorkflowDraftCreateFromTextTool>();
        services.AddScoped<IAiKernelFunction, WorkflowDraftPatchTool>();
        services.AddScoped<IAiKernelFunction, WorkflowBpmnGenerateDraftTool>();
        services.AddScoped<IAiKernelFunction, WorkflowBusinessCanvasGenerateDraftTool>();
        services.AddScoped<IAiKernelFunction, WorkflowBindingCreateDraftTool>();
        services.AddScoped<IAiKernelFunction, WorkflowFormPermissionSuggestTool>();
        services.AddScoped<IAiKernelFunction, WorkflowActionMapSuggestTool>();
        services.AddScoped<IAiKernelFunction, WorkflowNotificationPreviewTool>();
        services.AddScoped<IAiKernelFunction, WorkflowModelValidateDraftTool>();
        services.AddScoped<IAiKernelFunction, WorkflowModelSimulateDraftTool>();
        services.AddScoped<IAiKernelFunction, WorkflowConditionValidateTool>();
        services.AddScoped<IAiKernelFunction, WorkflowPublishPrecheckTool>();
        services.AddScoped<IAiKernelFunction, WorkflowInstanceDiagnoseTool>();
        services.AddScoped<IAiKernelFunction, WorkflowTaskAssistTool>();
        services.AddScoped<IAiKernelFunction, WorkflowNotificationDiagnoseTool>();
        services.AddScoped<IAiKernelFunction, WorkflowRuntimeTraceTool>();
        services.AddScoped<IAiKernelFunction>(_ => new WorkflowHighRiskBlockedTool(AiWorkflowToolCodes.ModelPublish, "发布流程", "L4 高风险：必须人工发布"));
        services.AddScoped<IAiKernelFunction>(_ => new WorkflowHighRiskBlockedTool(AiWorkflowToolCodes.ModelActivate, "启用流程", "L4 高风险：必须人工启用"));
        services.AddScoped<IAiKernelFunction>(_ => new WorkflowHighRiskBlockedTool(AiWorkflowToolCodes.ModelDeactivate, "停用流程", "L4 高风险：必须人工停用"));
        services.AddScoped<IAiKernelFunction>(_ => new WorkflowHighRiskBlockedTool(AiWorkflowToolCodes.BindingApply, "应用业务绑定", "L4 高风险：必须人工应用绑定"));
        services.AddScoped<IAiKernelFunction>(_ => new WorkflowHighRiskBlockedTool(AiWorkflowToolCodes.TaskApprove, "审批真实待办", "L4 高风险：禁止 Agent 自动审批"));
        services.AddScoped<IAiKernelFunction>(_ => new WorkflowHighRiskBlockedTool(AiWorkflowToolCodes.TaskReject, "驳回真实待办", "L4 高风险：禁止 Agent 自动驳回"));
    }
}

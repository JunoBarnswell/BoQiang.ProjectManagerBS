using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowModelGetTool(IWorkflowModelAppService workflowModelService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.ModelGet,
        "获取流程详情",
        "获取流程 BPMN、画布扩展、发布版本和模型状态",
        "L1",
        PermissionCodes.AiToolWorkflowRead,
        PermissionCodes.WorkflowModelQuery,
        ["Ask", "Plan", "Agent"],
        ["modelId"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var modelId = AiWorkflowArgumentReader.ReadString(context.Arguments, "modelId")!;
        var detail = await workflowModelService.GetDetailAsync(modelId, cancellationToken);
        return Result($"已读取流程模型：{detail.Name}", detail, Evidence(("modelId", modelId)));
    }
}

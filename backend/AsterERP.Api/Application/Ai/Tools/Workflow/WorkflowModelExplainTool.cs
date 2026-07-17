using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowModelExplainTool(IWorkflowModelAppService workflowModelService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.ModelExplain,
        "解释流程模型",
        "将流程模型结构解释为自然语言摘要",
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
        var explanation = new
        {
            detail.ModelId,
            detail.ModelKey,
            detail.Name,
            detail.Status,
            detail.Version,
            summary = $"流程《{detail.Name}》属于 {detail.AppCode}/{detail.CategoryCode}，当前模型状态 {detail.Status}，最新发布版本 {detail.Version?.ToString() ?? "未发布"}。"
        };
        return Result($"已解释流程模型：{detail.Name}", explanation, Evidence(("modelId", modelId)));
    }
}

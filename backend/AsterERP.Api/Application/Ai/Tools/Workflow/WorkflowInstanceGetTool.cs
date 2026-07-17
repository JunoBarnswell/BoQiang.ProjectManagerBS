using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowInstanceGetTool(IWorkflowInstanceAppService instanceService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.InstanceGet,
        "获取流程实例",
        "读取实例运行路径、当前节点、历史轨迹和通知记录",
        "L1",
        PermissionCodes.AiToolWorkflowRead,
        PermissionCodes.WorkflowInstanceQuery,
        ["Ask", "Agent"],
        ["processInstanceId"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var processInstanceId = AiWorkflowArgumentReader.ReadString(context.Arguments, "processInstanceId")!;
        var detail = await instanceService.GetDetailAsync(processInstanceId, cancellationToken);
        return Result($"已读取流程实例：{processInstanceId}", detail, Evidence(("processInstanceId", processInstanceId)));
    }
}

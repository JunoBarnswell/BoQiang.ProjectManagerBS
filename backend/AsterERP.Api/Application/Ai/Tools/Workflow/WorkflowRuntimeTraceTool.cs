using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowRuntimeTraceTool(IWorkflowInstanceAppService instanceService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.RuntimeTrace,
        "运行轨迹追踪",
        "输出实例节点轨迹、耗时和异常位置",
        "L1",
        PermissionCodes.AiToolWorkflowDiagnose,
        PermissionCodes.WorkflowInstanceQuery,
        ["Ask", "Agent"],
        ["processInstanceId"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var processInstanceId = AiWorkflowArgumentReader.ReadString(context.Arguments, "processInstanceId")!;
        var detail = await instanceService.GetDetailAsync(processInstanceId, cancellationToken);
        var payload = new
        {
            detail.ProcessInstanceId,
            detail.Status,
            detail.Timeline,
            activeTasks = detail.RuntimeTasks
        };
        return Result($"已输出流程实例 {processInstanceId} 的运行轨迹", payload, Evidence(("processInstanceId", processInstanceId)));
    }
}

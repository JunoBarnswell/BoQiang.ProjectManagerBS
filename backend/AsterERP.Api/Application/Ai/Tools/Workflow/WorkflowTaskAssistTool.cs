using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowTaskAssistTool(IWorkflowTaskAppService taskService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.TaskAssist,
        "待办处理建议",
        "解释当前待办内容和可选动作，不执行审批",
        "L1",
        PermissionCodes.AiToolWorkflowDiagnose,
        PermissionCodes.WorkflowTaskQuery,
        ["Ask", "Agent"],
        ["taskId"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var taskId = AiWorkflowArgumentReader.ReadString(context.Arguments, "taskId")!;
        var detail = await taskService.GetDetailAsync(taskId, cancellationToken);
        var payload = new
        {
            task = detail.Task,
            submittedForm = detail.SubmittedForm,
            availableActions = detail.Task.AvailableActions,
            advice = "请根据业务单据、审批意见和附件人工选择通过、驳回、转办或评论。AI 不会代你执行审批动作。"
        };
        return Result($"已生成待办 {taskId} 的处理建议", payload, Evidence(("taskId", taskId)));
    }
}

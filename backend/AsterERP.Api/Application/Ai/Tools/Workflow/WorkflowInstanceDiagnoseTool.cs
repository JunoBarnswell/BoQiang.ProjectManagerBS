using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowInstanceDiagnoseTool(
    IWorkflowInstanceAppService instanceService,
    AiWorkflowArtifactService artifactService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.InstanceDiagnose,
        "诊断流程实例",
        "只读分析实例卡住、节点未流转或条件未命中的原因",
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
        var evidence = new List<string>
        {
            $"实例状态：{detail.Status}",
            $"运行任务数：{detail.RuntimeTasks.Count}",
            $"轨迹节点数：{detail.Timeline.Count}",
            $"通知记录数：{detail.Notifications.Count}"
        };
        var suggestions = detail.RuntimeTasks.Count == 0
            ? ["当前没有运行中待办，检查实例是否已结束或异常终止。"]
            : detail.RuntimeTasks.Select(task => $"检查待办 {task.Id}/{task.Name} 的候选人、负责人和超时状态。").ToList();
        var report = await artifactService.SaveDiagnosisReportAsync(
            context,
            "instance",
            processInstanceId,
            $"实例 {processInstanceId} 诊断完成",
            evidence,
            suggestions,
            cancellationToken);
        var dto = AiWorkflowArtifactService.MapDiagnosis(report);
        return Result(
            dto.Summary,
            dto,
            Evidence(("diagnosisReportId", report.Id), ("processInstanceId", processInstanceId)),
            [Event("workflow_instance_diagnosed", "流程实例诊断完成", new { diagnosisReportId = report.Id, processInstanceId })]);
    }
}

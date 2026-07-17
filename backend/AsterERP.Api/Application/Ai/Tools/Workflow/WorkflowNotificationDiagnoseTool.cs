using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowNotificationDiagnoseTool(
    IWorkflowNotificationAppService notificationService,
    AiWorkflowArtifactService artifactService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.NotificationDiagnose,
        "通知失败诊断",
        "只读分析通知任务、日志、模板与渠道配置",
        "L1",
        PermissionCodes.AiToolWorkflowDiagnose,
        PermissionCodes.WorkflowNotificationLogQuery,
        ["Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var processInstanceId = AiWorkflowArgumentReader.ReadString(context.Arguments, "processInstanceId");
        var taskId = AiWorkflowArgumentReader.ReadString(context.Arguments, "workflowTaskId");
        var logs = await notificationService.GetLogsAsync(new WorkflowNotificationQuery(
            PageIndex: 1,
            PageSize: 50,
            ProcessInstanceId: processInstanceId,
            WorkflowTaskId: taskId), cancellationToken);
        var evidence = logs.Items.Select(item => $"{item.CreatedTime:u} {item.ChannelCode} {item.Result} {item.ErrorMessage}").ToList();
        var suggestions = logs.Total == 0
            ? new List<string> { "未找到通知日志，检查节点通知规则是否启用，以及触发时机是否匹配。" }
            : new List<string> { "检查失败日志中的渠道、收件人地址、模板变量和 provider trace。" };
        var targetId = processInstanceId ?? taskId ?? "notification";
        var report = await artifactService.SaveDiagnosisReportAsync(context, "notification", targetId, "通知诊断完成", evidence, suggestions, cancellationToken);
        return Result("通知诊断完成", AiWorkflowArtifactService.MapDiagnosis(report), Evidence(("diagnosisReportId", report.Id)));
    }
}

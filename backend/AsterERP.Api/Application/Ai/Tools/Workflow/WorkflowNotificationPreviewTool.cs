using System.Text.Json;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowNotificationPreviewTool(AiWorkflowArtifactService artifactService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.NotificationPreview,
        "生成通知模板预览",
        "预览节点通知内容，不写通知任务、不调用发送器",
        "L2",
        PermissionCodes.AiToolWorkflowDraft,
        PermissionCodes.WorkflowNotificationTemplateQuery,
        ["Plan", "Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var artifact = await artifactService.RequireDraftFromArgumentsAsync(context, cancellationToken);
        var draft = artifactService.ParseDraft(artifact);
        var previews = draft.Nodes
            .Where(item => item.Type.Equals("userTask", StringComparison.OrdinalIgnoreCase))
            .Select(node => new AiWorkflowNotificationPreviewDto
            {
                NodeId = node.Id,
                Trigger = "node-enter",
                ReceiverType = "candidate-role",
                TemplateCode = $"{draft.WorkflowKey}_{node.Id}_enter",
                Subject = $"待审批：{draft.WorkflowName}",
                Content = $"流程《{draft.WorkflowName}》已到达「{node.Name}」，请在 AsterERP 待办中处理。"
            })
            .ToList();
        artifact = await artifactService.UpdateDraftAsync(
            artifact,
            item => item.NotificationPreviewJson = JsonSerializer.Serialize(previews, WorkflowJsonOptions.Options),
            cancellationToken);
        return Result(
            "已生成通知模板预览",
            new { draftArtifact = AiWorkflowArtifactService.MapDraft(artifact), previews },
            Evidence(("draftArtifactId", artifact.Id)),
            [Event("workflow_notification_previewed", "已生成通知模板预览", new { draftArtifactId = artifact.Id })]);
    }
}

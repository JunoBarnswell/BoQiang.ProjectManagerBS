using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowPublishPrecheckTool(
    AiWorkflowArtifactService artifactService,
    WorkflowDraftValidator validator) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.PublishPrecheck,
        "发布前审查",
        "审查 AI 草稿是否满足人工发布前条件，不发布",
        "L1",
        PermissionCodes.AiToolWorkflowPublishRequest,
        PermissionCodes.WorkflowModelQuery,
        ["Agent", "Manual"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var artifact = await artifactService.RequireDraftFromArgumentsAsync(context, cancellationToken);
        var draft = artifactService.ParseDraft(artifact);
        var issues = await validator.ValidateAsync(draft, context, cancellationToken);
        var payload = new
        {
            draftArtifactId = artifact.Id,
            canRequestManualPublish = issues.All(item => item.Severity != "Error") && artifact.ImportedWorkflowModelId is not null,
            blockers = issues.Where(item => item.Severity == "Error"),
            manualSteps = new[]
            {
                "人工导入 AI 草稿为正式 Workflow 草稿",
                "在 Workflow 设计器中复核 BPMN、候选人、条件和通知",
                "由具备 workflow:model:publish 权限的用户人工发布"
            }
        };
        return Result(
            "发布前审查完成，未执行发布",
            payload,
            Evidence(("draftArtifactId", artifact.Id)),
            [Event("workflow_publish_precheck_completed", "发布前审查完成", new { draftArtifactId = artifact.Id })]);
    }
}

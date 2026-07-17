using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowBusinessCanvasGenerateDraftTool(
    AiWorkflowArtifactService artifactService,
    WorkflowBusinessCanvasDraftMapper canvasMapper) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.BusinessCanvasGenerateDraft,
        "生成业务画布草稿",
        "生成 Workflow 业务画布 JSON 草稿",
        "L2",
        PermissionCodes.AiToolWorkflowDraft,
        PermissionCodes.WorkflowModelEdit,
        ["Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var artifact = await artifactService.RequireDraftFromArgumentsAsync(context, cancellationToken);
        var draft = artifactService.ParseDraft(artifact);
        var canvas = canvasMapper.Map(draft);
        artifact = await artifactService.UpdateDraftAsync(artifact, item => item.BusinessCanvasJson = canvas, cancellationToken);
        return Result(
            "已生成业务画布 JSON 草稿",
            new { draftArtifact = AiWorkflowArtifactService.MapDraft(artifact), businessCanvasJson = canvas },
            Evidence(("draftArtifactId", artifact.Id)),
            [Event("workflow_canvas_generated", "已生成业务画布草稿", new { draftArtifactId = artifact.Id })]);
    }
}

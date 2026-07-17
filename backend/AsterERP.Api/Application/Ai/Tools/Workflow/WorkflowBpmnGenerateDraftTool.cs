using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowBpmnGenerateDraftTool(
    AiWorkflowArtifactService artifactService,
    WorkflowBpmnDraftMapper bpmnMapper) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.BpmnGenerateDraft,
        "生成 BPMN 草稿",
        "根据 AI Workflow DSL 生成 BPMN XML 草稿",
        "L2",
        PermissionCodes.AiToolWorkflowDraft,
        PermissionCodes.WorkflowModelEdit,
        ["Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var artifact = await artifactService.RequireDraftFromArgumentsAsync(context, cancellationToken);
        var draft = artifactService.ParseDraft(artifact);
        var bpmn = bpmnMapper.Map(draft);
        artifact = await artifactService.UpdateDraftAsync(artifact, item => item.BpmnXml = bpmn, cancellationToken);
        return Result(
            "已生成 BPMN XML 草稿",
            new { draftArtifact = AiWorkflowArtifactService.MapDraft(artifact), bpmnXml = bpmn },
            Evidence(("draftArtifactId", artifact.Id)),
            [Event("workflow_bpmn_generated", "已生成 BPMN 草稿", new { draftArtifactId = artifact.Id })]);
    }
}

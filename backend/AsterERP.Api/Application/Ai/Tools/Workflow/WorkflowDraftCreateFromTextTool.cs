using System.Text.Json;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowDraftCreateFromTextTool(
    WorkflowDraftParser parser,
    WorkflowBpmnDraftMapper bpmnMapper,
    WorkflowBusinessCanvasDraftMapper canvasMapper,
    AiWorkflowArtifactService artifactService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.ModelCreateDraftFromText,
        "根据自然语言创建流程草稿",
        "从已批准计划或用户描述生成 AI Workflow 草稿，不发布、不写正式流程表",
        "L2",
        PermissionCodes.AiToolWorkflowDraft,
        PermissionCodes.WorkflowModelAdd,
        ["Agent"],
        ["requirementText"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var text = AiWorkflowArgumentReader.ReadString(context.Arguments, "requirementText") ?? context.UserInstruction ?? string.Empty;
        var businessType = AiWorkflowArgumentReader.ReadString(context.Arguments, "businessType");
        var draft = parser.Parse(text, businessType);
        var bpmn = bpmnMapper.Map(draft);
        var canvas = canvasMapper.Map(draft);
        var artifact = await artifactService.CreateDraftAsync(context, draft, bpmn, canvas, cancellationToken);
        var payload = new
        {
            draftArtifact = AiWorkflowArtifactService.MapDraft(artifact),
            draft,
            validationHint = "请继续调用 workflow.model.validateDraft 与 workflow.model.simulateDraft"
        };
        return Result(
            $"已创建 AI Workflow 草稿：{draft.WorkflowName}",
            payload,
            Evidence(("draftArtifactId", artifact.Id), ("workflowKey", draft.WorkflowKey)),
            [
                Event("workflow_draft_created", $"已创建 Workflow 草稿：{draft.WorkflowName}", new { draftArtifactId = artifact.Id, draft.WorkflowKey }),
                Event("workflow_bpmn_generated", "已生成 BPMN 草稿", new { draftArtifactId = artifact.Id }),
                Event("workflow_canvas_generated", "已生成业务画布草稿", new { draftArtifactId = artifact.Id })
            ]);
    }
}

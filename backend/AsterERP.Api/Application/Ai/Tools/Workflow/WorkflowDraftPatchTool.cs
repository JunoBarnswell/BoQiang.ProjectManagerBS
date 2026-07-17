using System.Text.Json;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowDraftPatchTool(
    AiWorkflowArtifactService artifactService,
    WorkflowBpmnDraftMapper bpmnMapper,
    WorkflowBusinessCanvasDraftMapper canvasMapper) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.ModelPatchDraft,
        "修改流程草稿",
        "在 AI 草稿表内修改 Workflow 草稿，不覆盖正式流程",
        "L2",
        PermissionCodes.AiToolWorkflowDraft,
        PermissionCodes.WorkflowModelEdit,
        ["Agent", "Manual"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var artifact = await artifactService.RequireDraftFromArgumentsAsync(context, cancellationToken);
        var draft = artifactService.ParseDraft(artifact);
        var name = AiWorkflowArgumentReader.ReadString(context.Arguments, "workflowName");
        var nodes = AiWorkflowArgumentReader.ReadString(context.Arguments, "nodesJson");
        var edges = AiWorkflowArgumentReader.ReadString(context.Arguments, "edgesJson");
        if (!string.IsNullOrWhiteSpace(name))
        {
            draft.WorkflowName = name;
        }

        if (!string.IsNullOrWhiteSpace(nodes))
        {
            draft.Nodes = JsonSerializer.Deserialize<List<AiWorkflowDraftNodeDto>>(nodes, WorkflowJsonOptions.Options) ?? draft.Nodes;
        }

        if (!string.IsNullOrWhiteSpace(edges))
        {
            draft.Edges = JsonSerializer.Deserialize<List<AiWorkflowDraftEdgeDto>>(edges, WorkflowJsonOptions.Options) ?? draft.Edges;
        }

        artifact = await artifactService.UpdateDraftAsync(artifact, item =>
        {
            item.WorkflowName = draft.WorkflowName;
            item.DraftDslJson = JsonSerializer.Serialize(draft, WorkflowJsonOptions.Options);
            item.BpmnXml = bpmnMapper.Map(draft);
            item.BusinessCanvasJson = canvasMapper.Map(draft);
        }, cancellationToken);

        return Result(
            $"已更新 AI Workflow 草稿：{draft.WorkflowName}",
            new { draftArtifact = AiWorkflowArtifactService.MapDraft(artifact), draft },
            Evidence(("draftArtifactId", artifact.Id)),
            [Event("workflow_draft_updated", $"已更新 Workflow 草稿：{draft.WorkflowName}", new { draftArtifactId = artifact.Id })]);
    }
}

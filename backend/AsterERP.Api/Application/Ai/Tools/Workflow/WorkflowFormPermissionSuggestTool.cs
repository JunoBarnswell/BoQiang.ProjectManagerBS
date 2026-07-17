using System.Text.Json;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowFormPermissionSuggestTool(AiWorkflowArtifactService artifactService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.FormPermissionSuggest,
        "生成表单权限建议",
        "按节点生成字段可见/可编辑/必填建议，不写正式权限",
        "L2",
        PermissionCodes.AiToolWorkflowDraft,
        PermissionCodes.WorkflowModelQuery,
        ["Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var artifact = await artifactService.RequireDraftFromArgumentsAsync(context, cancellationToken);
        var draft = artifactService.ParseDraft(artifact);
        var fields = AiWorkflowArgumentReader.ReadStringList(context.Arguments, "fields");
        if (fields.Count == 0)
        {
            fields = ["amount", "remark", "attachment"];
        }

        var proposal = draft.Nodes
            .Where(item => item.Type.Equals("userTask", StringComparison.OrdinalIgnoreCase))
            .Select(node => new
            {
                nodeId = node.Id,
                nodeName = node.Name,
                fields = fields.Select(field => new
                {
                    field,
                    visible = true,
                    editable = field is "remark" or "attachment",
                    required = field == "remark"
                })
            })
            .ToList();
        artifact = await artifactService.UpdateDraftAsync(
            artifact,
            item => item.FormPermissionProposalJson = JsonSerializer.Serialize(proposal, WorkflowJsonOptions.Options),
            cancellationToken);
        return Result(
            "已生成表单权限建议",
            new { draftArtifact = AiWorkflowArtifactService.MapDraft(artifact), proposal },
            Evidence(("draftArtifactId", artifact.Id)),
            [Event("workflow_form_permission_suggested", "已生成表单权限建议", new { draftArtifactId = artifact.Id })]);
    }
}

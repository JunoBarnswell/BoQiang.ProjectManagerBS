using System.Text.Json;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowActionMapSuggestTool(AiWorkflowArtifactService artifactService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.ActionMapSuggest,
        "生成业务动作映射建议",
        "建议提交、撤回、通过、驳回、终止等动作与业务状态映射",
        "L2",
        PermissionCodes.AiToolWorkflowDraft,
        PermissionCodes.WorkflowModelQuery,
        ["Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var artifact = await artifactService.RequireDraftFromArgumentsAsync(context, cancellationToken);
        var proposal = new[]
        {
            new { action = "submit", fromStatus = "Draft", toStatus = "Approving", sideEffect = "start workflow instance" },
            new { action = "approve", fromStatus = "Approving", toStatus = "Approved", sideEffect = "complete workflow task manually" },
            new { action = "reject", fromStatus = "Approving", toStatus = "Rejected", sideEffect = "complete workflow task manually" },
            new { action = "withdraw", fromStatus = "Approving", toStatus = "Withdrawn", sideEffect = "withdraw instance manually" }
        };
        artifact = await artifactService.UpdateDraftAsync(
            artifact,
            item => item.ActionMappingProposalJson = JsonSerializer.Serialize(proposal, WorkflowJsonOptions.Options),
            cancellationToken);
        return Result("已生成业务动作映射建议", new { draftArtifact = AiWorkflowArtifactService.MapDraft(artifact), proposal }, Evidence(("draftArtifactId", artifact.Id)));
    }
}

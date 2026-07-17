using System.Text.Json;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowBindingCreateDraftTool(AiWorkflowArtifactService artifactService) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.BindingCreateDraft,
        "创建业务绑定草稿",
        "生成业务绑定建议 JSON，不写 workflow_bindings",
        "L2",
        PermissionCodes.AiToolWorkflowDraft,
        PermissionCodes.WorkflowBindingQuery,
        ["Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var artifact = await artifactService.RequireDraftFromArgumentsAsync(context, cancellationToken);
        var proposal = new AiWorkflowBindingProposalDto
        {
            BusinessType = AiWorkflowArgumentReader.ReadString(context.Arguments, "businessType") ?? artifact.BusinessType,
            MenuCode = AiWorkflowArgumentReader.ReadString(context.Arguments, "menuCode") ?? "ai:workflow:pending-binding",
            ProcessDefinitionKey = artifact.WorkflowKey,
            FormResourceCode = AiWorkflowArgumentReader.ReadString(context.Arguments, "formResourceCode"),
            PageCode = AiWorkflowArgumentReader.ReadString(context.Arguments, "pageCode"),
            ModelCode = AiWorkflowArgumentReader.ReadString(context.Arguments, "modelCode"),
            KeyField = AiWorkflowArgumentReader.ReadString(context.Arguments, "keyField") ?? "Id",
            Summary = "此绑定为 AI 建议草稿，需人工在 Workflow 绑定管理中确认后应用。"
        };
        artifact = await artifactService.UpdateDraftAsync(
            artifact,
            item => item.BindingProposalJson = JsonSerializer.Serialize(proposal, WorkflowJsonOptions.Options),
            cancellationToken);
        return Result(
            "已生成 Workflow 业务绑定建议",
            new { draftArtifact = AiWorkflowArtifactService.MapDraft(artifact), proposal },
            Evidence(("draftArtifactId", artifact.Id)),
            [Event("workflow_binding_suggested", "已生成业务绑定建议", new { draftArtifactId = artifact.Id })]);
    }
}

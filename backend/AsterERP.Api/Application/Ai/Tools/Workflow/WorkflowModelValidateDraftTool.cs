using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowModelValidateDraftTool(
    AiWorkflowArtifactService artifactService,
    WorkflowDraftValidator validator) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.ModelValidateDraft,
        "校验流程草稿",
        "校验 AI Workflow 草稿结构、节点、条件、角色与绑定风险",
        "L1",
        PermissionCodes.AiToolWorkflowValidate,
        PermissionCodes.WorkflowModelQuery,
        ["Plan", "Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var artifact = await artifactService.RequireDraftFromArgumentsAsync(context, cancellationToken);
        var draft = artifactService.ParseDraft(artifact);
        var issues = await validator.ValidateAsync(draft, context, cancellationToken);
        var report = await artifactService.SaveValidationReportAsync(context, artifact, issues, cancellationToken);
        var dto = AiWorkflowArtifactService.MapValidation(report);
        return Result(
            dto.IsValid ? "流程草稿校验通过" : $"流程草稿校验发现 {dto.ErrorCount} 个错误、{dto.WarningCount} 个警告",
            dto,
            Evidence(("draftArtifactId", artifact.Id), ("validationReportId", report.Id)),
            [
                Event("workflow_validation_started", "开始校验 Workflow 草稿", new { draftArtifactId = artifact.Id }),
                Event("workflow_validation_completed", "Workflow 草稿校验完成", new { draftArtifactId = artifact.Id, validationReportId = report.Id, dto.IsValid })
            ]);
    }
}

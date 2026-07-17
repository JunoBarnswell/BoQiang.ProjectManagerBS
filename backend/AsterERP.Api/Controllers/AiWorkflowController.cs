using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Application.Ai.Tools.Workflow;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/workflow")]
public sealed class AiWorkflowController(
    AiWorkflowArtifactService artifactService,
    AiKernelFunctionService toolExecutor) : BaseApiController
{
    [HttpGet("conversations/{conversationId}/overview")]
    [Permission(PermissionCodes.AiToolWorkflowView)]
    public async Task<IActionResult> GetOverviewAsync(string conversationId, CancellationToken cancellationToken)
    {
        return ApiOk(await artifactService.GetOverviewAsync(conversationId, cancellationToken));
    }

    [HttpGet("draft-artifacts/{id}")]
    [Permission(PermissionCodes.AiToolWorkflowView)]
    public async Task<IActionResult> GetDraftAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await artifactService.GetDraftAsync(id, cancellationToken));
    }

    [HttpGet("validation-reports/{id}")]
    [Permission(PermissionCodes.AiToolWorkflowView)]
    public async Task<IActionResult> GetValidationReportAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await artifactService.GetValidationReportAsync(id, cancellationToken));
    }

    [HttpGet("simulation-reports/{id}")]
    [Permission(PermissionCodes.AiToolWorkflowView)]
    public async Task<IActionResult> GetSimulationReportAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await artifactService.GetSimulationReportAsync(id, cancellationToken));
    }

    [HttpGet("diagnosis-reports/{id}")]
    [Permission(PermissionCodes.AiToolWorkflowView)]
    public async Task<IActionResult> GetDiagnosisReportAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await artifactService.GetDiagnosisReportAsync(id, cancellationToken));
    }

    [HttpPost("draft-artifacts/{id}/validate")]
    [Permission(PermissionCodes.AiToolWorkflowValidate)]
    public async Task<IActionResult> ValidateDraftAsync(string id, [FromBody] AiToolInvokeRequest request, CancellationToken cancellationToken)
    {
        request.Arguments["draftArtifactId"] = id;
        return ApiOk(await toolExecutor.InvokeAsync(AiWorkflowToolCodes.ModelValidateDraft, request, cancellationToken));
    }

    [HttpPost("draft-artifacts/{id}/simulate")]
    [Permission(PermissionCodes.AiToolWorkflowSimulate)]
    public async Task<IActionResult> SimulateDraftAsync(string id, [FromBody] AiToolInvokeRequest request, CancellationToken cancellationToken)
    {
        request.Arguments["draftArtifactId"] = id;
        return ApiOk(await toolExecutor.InvokeAsync(AiWorkflowToolCodes.ModelSimulateDraft, request, cancellationToken));
    }

    [HttpPost("draft-artifacts/{id}/open-in-designer")]
    [Permission(PermissionCodes.AiToolWorkflowView)]
    public IActionResult OpenInDesigner(string id)
    {
        return ApiOk(new
        {
            draftArtifactId = id,
            designerRoute = $"/workflow/models?aiDraftArtifactId={Uri.EscapeDataString(id)}"
        });
    }
}

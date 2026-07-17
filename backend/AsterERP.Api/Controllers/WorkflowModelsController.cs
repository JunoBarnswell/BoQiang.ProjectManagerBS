using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/models")]
public sealed class WorkflowModelsController(IWorkflowModelAppService workflowModelService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.WorkflowModelQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowModelService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("{modelId}")]
    [Permission(PermissionCodes.WorkflowModelQuery)]
    public async Task<IActionResult> GetDetailAsync(string modelId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowModelService.GetDetailAsync(modelId, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.WorkflowModelAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] WorkflowModelUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowModelService.CreateOrUpdateAsync(request, cancellationToken));
    }

    [HttpPut("{modelId}")]
    [Permission(PermissionCodes.WorkflowModelEdit)]
    public async Task<IActionResult> UpdateAsync(string modelId, [FromBody] WorkflowModelUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowModelService.CreateOrUpdateAsync(request with { ModelId = modelId }, cancellationToken));
    }

    [HttpDelete("{modelId}/draft")]
    [Permission(PermissionCodes.WorkflowModelDelete)]
    public async Task<IActionResult> DeleteDraftAsync(string modelId, CancellationToken cancellationToken)
    {
        await workflowModelService.DeleteDraftAsync(modelId, cancellationToken);
        return ApiOk(true);
    }

    [HttpPut("{modelId}/xml")]
    [Permission(PermissionCodes.WorkflowModelEdit)]
    public async Task<IActionResult> SaveXmlAsync(string modelId, [FromBody] WorkflowModelXmlSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowModelService.SaveXmlAsync(modelId, request, cancellationToken));
    }

    [HttpPost("import-ai-draft/{draftArtifactId}")]
    [Permission(PermissionCodes.AiToolWorkflowImportDraft)]
    public async Task<IActionResult> ImportAiDraftAsync(string draftArtifactId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowModelService.ImportAiDraftAsync(draftArtifactId, cancellationToken));
    }

    [HttpPost("{modelId}/validate")]
    [Permission(PermissionCodes.WorkflowModelEdit)]
    public async Task<IActionResult> ValidateAsync(string modelId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowModelService.ValidateAsync(modelId, cancellationToken));
    }

    [HttpPost("{modelId}/publish")]
    [Permission(PermissionCodes.WorkflowModelPublish)]
    public async Task<IActionResult> PublishAsync(string modelId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowModelService.PublishAsync(modelId, cancellationToken));
    }

    [HttpPost("definitions/{processDefinitionId}/suspend")]
    [Permission(PermissionCodes.WorkflowModelSuspend)]
    public async Task<IActionResult> SuspendAsync(string processDefinitionId, CancellationToken cancellationToken)
    {
        await workflowModelService.SuspendAsync(processDefinitionId, cancellationToken);
        return ApiOk(true);
    }

    [HttpGet("{modelKey}/versions")]
    [Permission(PermissionCodes.WorkflowModelQuery)]
    public async Task<IActionResult> GetVersionsAsync(string modelKey, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowModelService.GetVersionsAsync(modelKey, cancellationToken));
    }
}

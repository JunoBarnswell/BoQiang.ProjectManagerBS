using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai")]
public sealed class AiToolsController(AiKernelFunctionService toolExecutor, AiToolManagementService toolManagementService) : BaseApiController
{
    [HttpGet("tools")]
    [Permission(PermissionCodes.AiToolView)]
    public IActionResult GetTools()
    {
        return ApiOk(toolExecutor.ListDefinitions());
    }

    [HttpGet("tools/{toolCode}")]
    [Permission(PermissionCodes.AiToolView)]
    public IActionResult GetTool(string toolCode)
    {
        return ApiOk(toolExecutor.GetDefinition(toolCode));
    }

    [HttpPost("tools/{toolCode}/dry-run")]
    [Permission(PermissionCodes.AiToolView)]
    public async Task<IActionResult> DryRunAsync(
        string toolCode,
        [FromBody] AiToolInvokeRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await toolExecutor.DryRunAsync(toolCode, request, cancellationToken));
    }

    [HttpPost("tools/{toolCode}/invoke")]
    [Permission(PermissionCodes.AiToolView)]
    public async Task<IActionResult> InvokeAsync(
        string toolCode,
        [FromBody] AiToolInvokeRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await toolExecutor.InvokeAsync(toolCode, request, cancellationToken));
    }

    [HttpGet("runs/{runId}/tool-invocations")]
    [Permission(PermissionCodes.AiToolView)]
    public async Task<IActionResult> GetRunInvocationsAsync(string runId, CancellationToken cancellationToken)
    {
        return ApiOk(await toolExecutor.GetRunInvocationsAsync(runId, cancellationToken));
    }

    [HttpGet("tools/definitions")]
    [Permission(PermissionCodes.AiToolView)]
    public async Task<IActionResult> GetDefinitionsAsync([FromQuery] AiToolDefinitionQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await toolManagementService.GetDefinitionsAsync(query, cancellationToken));
    }

    [HttpPost("tools/definitions")]
    [Permission(PermissionCodes.AiToolAdd)]
    public async Task<IActionResult> CreateDefinitionAsync([FromBody] AiToolDefinitionUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await toolManagementService.UpsertDefinitionAsync(null, request, cancellationToken));
    }

    [HttpPut("tools/definitions/{id}")]
    [Permission(PermissionCodes.AiToolEdit)]
    public async Task<IActionResult> UpdateDefinitionAsync(string id, [FromBody] AiToolDefinitionUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await toolManagementService.UpsertDefinitionAsync(id, request, cancellationToken));
    }

    [HttpPost("tools/definitions/sync")]
    [Permission(PermissionCodes.AiToolEdit)]
    public async Task<IActionResult> SyncDefinitionsAsync(CancellationToken cancellationToken)
    {
        await toolManagementService.SyncCatalogDefinitionsAsync(cancellationToken);
        return ApiOk(true);
    }

    [HttpGet("tools/bindings")]
    [Permission(PermissionCodes.AiToolView)]
    public async Task<IActionResult> GetBindingsAsync([FromQuery] string? agentProfileId, CancellationToken cancellationToken)
    {
        return ApiOk(await toolManagementService.GetBindingsAsync(agentProfileId, cancellationToken));
    }

    [HttpPut("tools/bindings")]
    [Permission(PermissionCodes.AiToolEdit)]
    public async Task<IActionResult> UpsertBindingAsync([FromBody] AiToolBindingUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await toolManagementService.UpsertBindingAsync(request, cancellationToken));
    }

    [HttpGet("workflow-tools/available-workflows")]
    [Permission(PermissionCodes.AiToolBindWorkflow)]
    public async Task<IActionResult> GetAvailableWorkflowsAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await toolManagementService.GetAvailableWorkflowsAsync(cancellationToken));
    }

    [HttpPut("workflow-tools/bindings")]
    [Permission(PermissionCodes.AiToolBindWorkflow)]
    public async Task<IActionResult> BindWorkflowAsync([FromBody] AiWorkflowToolBindingRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await toolManagementService.BindWorkflowAsync(request, cancellationToken));
    }
}

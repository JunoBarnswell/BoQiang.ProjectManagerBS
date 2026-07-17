using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/bindings")]
public sealed class WorkflowBindingsController(IWorkflowBindingAppService workflowBindingService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.WorkflowBindingQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowBindingService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("{id}")]
    [Permission(PermissionCodes.WorkflowBindingQuery)]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowBindingService.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.WorkflowBindingEdit)]
    public async Task<IActionResult> SaveAsync([FromBody] WorkflowBindingUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowBindingService.SaveAsync(request, cancellationToken));
    }

    [HttpPost("status")]
    [Permission(PermissionCodes.WorkflowInstanceStart)]
    public async Task<IActionResult> GetStatusAsync([FromBody] WorkflowBindingStatusRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowBindingService.GetStatusAsync(request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.WorkflowBindingDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await workflowBindingService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }
}

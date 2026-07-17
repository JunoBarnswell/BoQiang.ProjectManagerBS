using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/delegations")]
public sealed class WorkflowDelegationsController(IWorkflowDelegationAppService workflowDelegationService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.WorkflowDelegationQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowDelegationService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.WorkflowDelegationEdit)]
    public async Task<IActionResult> SaveAsync([FromBody] WorkflowDelegationRuleUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowDelegationService.SaveAsync(request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.WorkflowDelegationDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await workflowDelegationService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }
}

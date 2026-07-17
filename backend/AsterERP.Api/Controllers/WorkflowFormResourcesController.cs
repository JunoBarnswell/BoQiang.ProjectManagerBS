using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/form-resources")]
public sealed class WorkflowFormResourcesController(IWorkflowFormResourceAppService workflowFormResourceService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.WorkflowFormQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowFormResourceService.GetPageAsync(gridQuery, cancellationToken));
    }
}

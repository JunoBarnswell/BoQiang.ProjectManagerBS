using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/participants")]
public sealed class WorkflowParticipantsController(IWorkflowParticipantAppService workflowParticipantService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.WorkflowParticipantQuery)]
    public async Task<IActionResult> QueryAsync([FromQuery] string? keyword, [FromQuery] string? type, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowParticipantService.QueryAsync(keyword, type, cancellationToken));
    }
}

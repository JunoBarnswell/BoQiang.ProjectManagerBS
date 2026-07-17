using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/reports")]
public sealed class WorkflowReportsController(IWorkflowReportAppService workflowReportService) : BaseApiController
{
    [HttpGet("overview")]
    [Permission(PermissionCodes.WorkflowReportQuery)]
    public async Task<IActionResult> GetOverviewAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await workflowReportService.GetOverviewAsync(cancellationToken));
    }
}

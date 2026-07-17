using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/deployments")]
public sealed class WorkflowDeploymentsController(IWorkflowDeploymentAppService workflowDeploymentService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.WorkflowDeploymentQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowDeploymentService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("definitions")]
    [Permission(PermissionCodes.WorkflowDeploymentQuery)]
    public async Task<IActionResult> GetProcessDefinitionsAsync([FromQuery] string? key, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowDeploymentService.GetProcessDefinitionsAsync(key, cancellationToken));
    }

    [HttpGet("{deploymentId}/resources/{resourceName}")]
    [Permission(PermissionCodes.WorkflowDeploymentResource)]
    public async Task<IActionResult> GetResourceAsync(string deploymentId, string resourceName, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowDeploymentService.GetResourceAsync(deploymentId, resourceName, cancellationToken));
    }
}

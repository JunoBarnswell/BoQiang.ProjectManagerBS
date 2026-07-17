using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/categories")]
public sealed class WorkflowCategoriesController(IWorkflowCategoryAppService workflowCategoryService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.WorkflowCategoryQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowCategoryService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.WorkflowCategoryEdit)]
    public async Task<IActionResult> SaveAsync([FromBody] WorkflowCategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowCategoryService.SaveAsync(request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.WorkflowCategoryDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await workflowCategoryService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }
}

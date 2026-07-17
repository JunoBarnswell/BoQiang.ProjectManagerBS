using AsterERP.Api.Application.System.QueryViews;
using AsterERP.Shared;
using AsterERP.Contracts.System.QueryViews;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/query-view-designers")]
public sealed class SystemQueryViewDesignerController(IQueryViewDesignerService designerService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemQueryViewDesign)]
    public async Task<IActionResult> GetListAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await designerService.GetListAsync(cancellationToken));
    }

    [HttpGet("{id}")]
    [Permission(PermissionCodes.SystemQueryViewDesign)]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await designerService.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.SystemQueryViewEdit)]
    public async Task<IActionResult> CreateAsync([FromBody] QueryViewDesignerSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await designerService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.SystemQueryViewEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] QueryViewDesignerSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await designerService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id}/preview-plan")]
    [Permission(PermissionCodes.SystemQueryViewPreview)]
    public async Task<IActionResult> PreviewPlanAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await designerService.PreviewPlanAsync(id, cancellationToken));
    }

    [HttpPost("{id}/preview-data")]
    [Permission(PermissionCodes.SystemQueryViewPreview)]
    public async Task<IActionResult> PreviewDataAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await designerService.PreviewDataAsync(id, cancellationToken));
    }

    [HttpPost("{id}/publish")]
    [Permission(PermissionCodes.SystemQueryViewPublish)]
    public async Task<IActionResult> PublishAsync(string id, [FromBody] QueryViewPublishRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await designerService.PublishAsync(id, request, cancellationToken));
    }

    [HttpPost("{id}/rollback")]
    [Permission(PermissionCodes.SystemQueryViewRollback)]
    public async Task<IActionResult> RollbackAsync(string id, [FromBody] QueryViewRollbackRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await designerService.RollbackAsync(id, request, cancellationToken));
    }

    [HttpGet("publish-logs")]
    [Permission(PermissionCodes.SystemQueryViewTask)]
    public async Task<IActionResult> GetPublishLogsAsync([FromQuery] string? viewId, CancellationToken cancellationToken)
    {
        return ApiOk(await designerService.GetPublishLogsAsync(viewId, cancellationToken));
    }
}

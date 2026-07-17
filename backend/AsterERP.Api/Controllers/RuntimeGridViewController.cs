using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/runtime/grid-views")]
public sealed class RuntimeGridViewController(
    IRuntimeGridViewService runtimeGridViewService,
    IRuntimePageSchemaService pageSchemaService) : BaseApiController
{
    [HttpGet("{pageCode}")]
    public async Task<IActionResult> GetAsync(
        string pageCode,
        [FromQuery] string? previewPageId,
        CancellationToken cancellationToken)
    {
        await pageSchemaService.GetPublishedPageAsync(pageCode, previewPageId, cancellationToken);
        return ApiOk(await runtimeGridViewService.GetAsync(pageCode, previewPageId, cancellationToken));
    }

    [HttpPost("{pageCode}/save-user-view")]
    [Permission(PermissionCodes.RuntimeGridViewSaveUser)]
    public async Task<IActionResult> SaveUserViewAsync(
        string pageCode,
        [FromBody] RuntimeGridViewSaveRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await runtimeGridViewService.SaveUserViewAsync(pageCode, request, cancellationToken));
    }

    [HttpPost("{pageCode}/save-tenant-default")]
    [Permission(PermissionCodes.RuntimeGridViewSaveTenant)]
    public async Task<IActionResult> SaveTenantDefaultAsync(
        string pageCode,
        [FromBody] RuntimeGridViewSaveRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await runtimeGridViewService.SaveTenantDefaultAsync(pageCode, request, cancellationToken));
    }

    [HttpPost("{pageCode}/reset-user-view")]
    [Permission(PermissionCodes.RuntimeGridViewSaveUser)]
    public async Task<IActionResult> ResetUserViewAsync(string pageCode, CancellationToken cancellationToken)
    {
        return ApiOk(await runtimeGridViewService.ResetUserViewAsync(pageCode, cancellationToken));
    }
}

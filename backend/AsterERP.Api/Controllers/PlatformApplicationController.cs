using AsterERP.Api.Application.Platform.Applications;
using AsterERP.Shared;
using AsterERP.Contracts.Platform;
using AsterERP.Contracts.System;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/platform/applications")]
public sealed class PlatformApplicationController(
    IPlatformApplicationService applicationService,
    IPlatformApplicationEntryService applicationEntryService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.PlatformApplicationQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await applicationService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.PlatformApplicationAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] ApplicationUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await applicationService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.PlatformApplicationEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] ApplicationUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await applicationService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.PlatformApplicationDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await applicationService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-status")]
    [Permission(PermissionCodes.PlatformApplicationEdit)]
    public async Task<IActionResult> BatchUpdateStatusAsync([FromBody] BatchStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await applicationService.BatchUpdateStatusAsync(request.Ids, request.Status, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{appCode}/enter")]
    [Permission(PermissionCodes.PlatformApplicationEnter)]
    public async Task<IActionResult> EnterAsync(
        string appCode,
        [FromBody] ApplicationBackendEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await applicationEntryService.EnterAsync(appCode, request, HttpContext, cancellationToken));
        }
        catch (ValidationException ex) when (ex.Code == ErrorCodes.AuthenticationRequired)
        {
            return Unauthorized(ApiResultFactory.Fail<object?>(ex.Message, HttpContext.TraceIdentifier, ex.Code));
        }
        catch (ValidationException ex) when (ex.Code == ErrorCodes.PermissionDenied)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResultFactory.Fail<object?>(ex.Message, HttpContext.TraceIdentifier, ex.Code));
        }
    }
}

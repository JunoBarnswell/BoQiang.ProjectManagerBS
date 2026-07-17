using AsterERP.Api.Application.System.Parameters;
using AsterERP.Shared;
using AsterERP.Contracts.System;
using AsterERP.Contracts.System.Parameters;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/parameters")]
public sealed class SystemParameterController(IParameterService parameterService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemParameterQuery)]
    public async Task<IActionResult> GetPageAsync(
        [FromQuery] GridQuery gridQuery,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        return ApiOk(await parameterService.GetPageAsync(gridQuery, category, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.SystemParameterAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] ParameterUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await parameterService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.SystemParameterEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] ParameterUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await parameterService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.SystemParameterDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await parameterService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-status")]
    [Permission(PermissionCodes.SystemParameterEdit)]
    public async Task<IActionResult> BatchUpdateStatusAsync([FromBody] BatchStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await parameterService.BatchUpdateStatusAsync(request.Ids, request.Status, cancellationToken);
        return ApiOk(true);
    }
}

using AsterERP.Api.Application.System.Dicts;
using AsterERP.Shared;
using AsterERP.Contracts.System.Dicts;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/dicts")]
public sealed class SystemDictionaryController(IDictManagementService dictManagementService) : BaseApiController
{
    [HttpGet("types")]
    [Permission(PermissionCodes.SystemDictQuery)]
    public async Task<IActionResult> GetTypesAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await dictManagementService.GetTypesPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("types/{id}")]
    [Permission(PermissionCodes.SystemDictQuery)]
    public async Task<IActionResult> GetTypeDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await dictManagementService.GetTypeDetailAsync(id, cancellationToken));
    }

    [HttpPost("types")]
    [Permission(PermissionCodes.SystemDictAdd)]
    public async Task<IActionResult> CreateTypeAsync([FromBody] DictTypeUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await dictManagementService.CreateTypeAsync(request, cancellationToken));
    }

    [HttpPut("types/{id}")]
    [Permission(PermissionCodes.SystemDictEdit)]
    public async Task<IActionResult> UpdateTypeAsync(string id, [FromBody] DictTypeUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await dictManagementService.UpdateTypeAsync(id, request, cancellationToken));
    }

    [HttpDelete("types/{id}")]
    [Permission(PermissionCodes.SystemDictDelete)]
    public async Task<IActionResult> DeleteTypeAsync(string id, CancellationToken cancellationToken)
    {
        await dictManagementService.DeleteTypeAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpGet("types/{dictTypeId}/items")]
    [Permission(PermissionCodes.SystemDictQuery)]
    public async Task<IActionResult> GetItemsAsync(string dictTypeId, CancellationToken cancellationToken)
    {
        return ApiOk(await dictManagementService.GetItemsAsync(dictTypeId, cancellationToken));
    }

    [HttpGet("items/{id}")]
    [Permission(PermissionCodes.SystemDictQuery)]
    public async Task<IActionResult> GetItemDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await dictManagementService.GetItemDetailAsync(id, cancellationToken));
    }

    [HttpPost("types/{dictTypeId}/items")]
    [Permission(PermissionCodes.SystemDictAdd)]
    public async Task<IActionResult> CreateItemAsync(string dictTypeId, [FromBody] DictItemUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await dictManagementService.CreateItemAsync(dictTypeId, request, cancellationToken));
    }

    [HttpPut("items/{id}")]
    [Permission(PermissionCodes.SystemDictEdit)]
    public async Task<IActionResult> UpdateItemAsync(string id, [FromBody] DictItemUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await dictManagementService.UpdateItemAsync(id, request, cancellationToken));
    }

    [HttpDelete("items/{id}")]
    [Permission(PermissionCodes.SystemDictDelete)]
    public async Task<IActionResult> DeleteItemAsync(string id, CancellationToken cancellationToken)
    {
        await dictManagementService.DeleteItemAsync(id, cancellationToken);
        return ApiOk(true);
    }
}

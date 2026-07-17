using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/providers")]
public sealed class AiModelProvidersController(IAiModelConfigurationService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AiProviderView)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetProvidersAsync(query, cancellationToken));
    }

    [HttpGet("options")]
    [Permission(PermissionCodes.AiProviderView)]
    public async Task<IActionResult> GetOptionsAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetProviderOptionsAsync(cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.AiProviderAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] AiProviderUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.CreateProviderAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.AiProviderEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] AiProviderUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.UpdateProviderAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.AiProviderDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await service.DeleteProviderAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{id}/test")]
    [Permission(PermissionCodes.AiProviderTest)]
    public async Task<IActionResult> TestAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.TestProviderAsync(id, cancellationToken));
    }

    [HttpPost("{id}/enable")]
    [Permission(PermissionCodes.AiProviderDisable)]
    public async Task<IActionResult> EnableAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.SetProviderStatusAsync(id, true, cancellationToken));
    }

    [HttpPost("{id}/disable")]
    [Permission(PermissionCodes.AiProviderDisable)]
    public async Task<IActionResult> DisableAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.SetProviderStatusAsync(id, false, cancellationToken));
    }
}

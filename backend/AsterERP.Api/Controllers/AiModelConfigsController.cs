using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/model-configs")]
public sealed class AiModelConfigsController(IAiModelConfigurationService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AiModelView)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetModelsAsync(query, cancellationToken));
    }

    [HttpGet("options")]
    [Permission(PermissionCodes.AiModelView)]
    public async Task<IActionResult> GetOptionsAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetModelOptionsAsync(cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.AiModelAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] AiModelConfigUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.CreateModelAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.AiModelEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] AiModelConfigUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.UpdateModelAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.AiModelDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await service.DeleteModelAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{id}/enable")]
    [Permission(PermissionCodes.AiModelDisable)]
    public async Task<IActionResult> EnableAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.SetModelStatusAsync(id, true, cancellationToken));
    }

    [HttpPost("{id}/disable")]
    [Permission(PermissionCodes.AiModelDisable)]
    public async Task<IActionResult> DisableAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.SetModelStatusAsync(id, false, cancellationToken));
    }

    [HttpPost("{id}/copy")]
    [Permission(PermissionCodes.AiModelCopy)]
    public async Task<IActionResult> CopyAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.CopyModelAsync(id, cancellationToken));
    }
}

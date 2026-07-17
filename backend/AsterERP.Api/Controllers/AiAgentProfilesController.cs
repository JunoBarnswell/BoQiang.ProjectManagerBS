using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/agents")]
public sealed class AiAgentProfilesController(IAiAgentProfileService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AiAgentView)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetPageAsync(query, cancellationToken));
    }

    [HttpGet("options")]
    [Permission(PermissionCodes.AiAgentView)]
    public async Task<IActionResult> GetOptionsAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetOptionsAsync(cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.AiAgentAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] AiAgentProfileUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.AiAgentEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] AiAgentProfileUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.AiAgentDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{id}/copy")]
    [Permission(PermissionCodes.AiAgentCopy)]
    public async Task<IActionResult> CopyAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.CopyAsync(id, cancellationToken));
    }

    [HttpPost("{id}/enable")]
    [Permission(PermissionCodes.AiAgentDisable)]
    public async Task<IActionResult> EnableAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.SetStatusAsync(id, true, cancellationToken));
    }

    [HttpPost("{id}/disable")]
    [Permission(PermissionCodes.AiAgentDisable)]
    public async Task<IActionResult> DisableAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.SetStatusAsync(id, false, cancellationToken));
    }

    [HttpPost("{id}/test")]
    [Permission(PermissionCodes.AiAgentTest)]
    public async Task<IActionResult> TestAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.TestAsync(id, cancellationToken));
    }
}

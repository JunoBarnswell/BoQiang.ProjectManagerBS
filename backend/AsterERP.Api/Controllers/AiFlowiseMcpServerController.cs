using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/flowise/mcp-server")]
public sealed class AiFlowiseMcpServerController(IFlowiseMcpServerService mcpServerService) : BaseApiController
{
    [HttpGet("{id}")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await mcpServerService.GetAsync(id, cancellationToken));
    }

    [HttpPost("{id}")]
    [Permission(PermissionCodes.FlowiseEdit)]
    public async Task<IActionResult> CreateAsync(string id, [FromBody] FlowiseMcpServerUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await mcpServerService.CreateAsync(id, request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.FlowiseEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] FlowiseMcpServerUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await mcpServerService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.FlowiseEdit)]
    public async Task<IActionResult> DisableAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await mcpServerService.DisableAsync(id, cancellationToken));
    }

    [HttpPost("{id}/refresh")]
    [Permission(PermissionCodes.FlowiseEdit)]
    public async Task<IActionResult> RefreshTokenAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await mcpServerService.RefreshTokenAsync(id, cancellationToken));
    }
}

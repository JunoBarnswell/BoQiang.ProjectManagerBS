using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/flowise/custom-mcp-servers")]
public sealed class AiFlowiseCustomMcpServersController(IFlowiseCustomMcpServerService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.FlowiseToolsView)]
    public async Task<IActionResult> GetPageAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
        => ApiOk(await service.GetPageAsync(query, cancellationToken));

    [HttpGet("{id}")]
    [Permission(PermissionCodes.FlowiseToolsView)]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.FlowiseToolsCreate)]
    public async Task<IActionResult> CreateAsync([FromBody] FlowiseCustomMcpServerUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.FlowiseToolsUpdate)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] FlowiseCustomMcpServerUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.FlowiseToolsDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await service.DeleteAsync(id, cancellationToken));

    [HttpPost("{id}/authorize")]
    [Permission(PermissionCodes.FlowiseToolsUpdate)]
    public async Task<IActionResult> AuthorizeAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await service.AuthorizeAsync(id, cancellationToken));

    [HttpGet("{id}/tools")]
    [Permission(PermissionCodes.FlowiseToolsView)]
    public async Task<IActionResult> GetToolsAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await service.GetToolsAsync(id, cancellationToken));
}

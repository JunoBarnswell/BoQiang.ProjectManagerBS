using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/flowise/nodes")]
public sealed class AiFlowiseNodesController(IFlowiseNodeCatalogService nodeCatalogService) : BaseApiController
{
    [HttpGet("definitions")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await nodeCatalogService.GetDefinitionsAsync(cancellationToken));
    }

    [HttpGet("icon/{name}")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> GetIconAsync(string name, CancellationToken cancellationToken)
    {
        var icon = await nodeCatalogService.GetNodeIconAsync(name, cancellationToken);
        return File(icon.Content, icon.ContentType, icon.FileName);
    }
}

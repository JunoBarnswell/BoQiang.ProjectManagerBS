using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[ApiController]
[Route("api/v1/node-icon")]
public sealed class AiFlowiseNodeIconsController(IFlowiseNodeCatalogService nodeCatalogService) : ControllerBase
{
    [HttpGet("{name}")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> GetSingleNodeIconAsync(string name, CancellationToken cancellationToken)
    {
        var icon = await nodeCatalogService.GetNodeIconAsync(name, cancellationToken);
        return File(icon.Content, icon.ContentType, icon.FileName);
    }
}

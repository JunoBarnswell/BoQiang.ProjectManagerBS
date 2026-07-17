using AsterERP.Api.Application.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/sk-capabilities")]
public sealed class AiSkCapabilitiesController(AiSkCapabilityService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AiCapabilityView)]
    public IActionResult GetCapabilities()
    {
        return ApiOk(service.ListCapabilities());
    }
}

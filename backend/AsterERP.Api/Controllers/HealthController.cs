using AsterERP.Api.Application.Health;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/health")]
public sealed class HealthController(HealthService healthService) : BaseApiController
{
    [HttpGet]
    public IActionResult Get()
    {
        return ApiOk(healthService.GetStatus());
    }
}

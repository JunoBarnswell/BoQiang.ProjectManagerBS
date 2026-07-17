using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult ApiOk<T>(T data, string message = "success")
    {
        return Ok(ApiResultFactory.Ok(data, HttpContext.TraceIdentifier, message));
    }
}

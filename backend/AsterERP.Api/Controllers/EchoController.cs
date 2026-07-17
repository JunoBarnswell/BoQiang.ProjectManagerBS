using AsterERP.Api.Application.Echo;
using AsterERP.Shared;
using AsterERP.Contracts.Echo;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/echo")]
public sealed class EchoController(EchoService echoService) : BaseApiController
{
    [HttpPost]
    public IActionResult Post([FromBody] EchoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(ApiResultFactory.Fail<object?>(
                "请求消息不能为空",
                HttpContext.TraceIdentifier,
                ErrorCodes.ParameterInvalid));
        }

        return ApiOk(echoService.CreateResponse(request.Message.Trim()));
    }
}

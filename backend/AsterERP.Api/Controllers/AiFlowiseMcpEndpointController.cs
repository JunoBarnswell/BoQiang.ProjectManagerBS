using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/v1/mcp")]
public sealed class AiFlowiseMcpEndpointController(IFlowiseMcpEndpointService mcpEndpointService) : ControllerBase
{
    [HttpPost("{chatflowId}")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> PostAsync(string chatflowId, [FromBody] FlowiseMcpJsonRpcRequest request, CancellationToken cancellationToken)
    {
        var token = ExtractBearerToken(Request.Headers.Authorization);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new FlowiseMcpJsonRpcResponse
            {
                Error = new FlowiseMcpJsonRpcError
                {
                    Code = -32001,
                    Message = "Unauthorized: missing or invalid Authorization header. Use Bearer <token>."
                }
            });
        }

        var response = await mcpEndpointService.HandleAsync(chatflowId, token, request, cancellationToken);
        if (response.Error?.Message == "Unauthorized")
        {
            return Unauthorized(response);
        }

        if (response.Error?.Code == -32001)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    [HttpDelete("{chatflowId}")]
    public IActionResult Delete(string chatflowId)
    {
        return StatusCode(StatusCodes.Status405MethodNotAllowed, new FlowiseMcpJsonRpcResponse
        {
            Error = new FlowiseMcpJsonRpcError
            {
                Code = -32000,
                Message = "Session termination is not supported in stateless mode."
            }
        });
    }

    private static string? ExtractBearerToken(string? authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization["Bearer ".Length..].Trim();
        return token.Length == 0 ? null : token;
    }
}

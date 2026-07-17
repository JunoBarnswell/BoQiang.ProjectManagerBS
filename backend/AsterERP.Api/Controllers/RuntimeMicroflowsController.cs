using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/runtime/microflows")]
public sealed class RuntimeMicroflowsController(
    IApplicationMicroflowRuntimeService service,
    ApplicationMicroflowContractService contractService,
    ApplicationMicroflowRuntimePermissionService permissionService) : BaseApiController
{
    [HttpGet("{flowCode}/contract")]
    public async Task<IActionResult> GetContractAsync(
        string flowCode,
        CancellationToken cancellationToken)
    {
        return ApiOk(await contractService.GetAsync(flowCode, cancellationToken));
    }

    [HttpGet("contracts")]
    public async Task<IActionResult> GetContractsAsync(
        [FromQuery] string? flowCodes,
        CancellationToken cancellationToken)
    {
        var codes = (flowCodes ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return ApiOk(await contractService.GetManyAsync(codes, cancellationToken));
    }

    [HttpPost("{flowCode}/execute")]
    public async Task<IActionResult> ExecuteAsync(
        string flowCode,
        [FromBody] ApplicationMicroflowExecuteRequest request,
        CancellationToken cancellationToken)
    {
        await permissionService.EnsureAsync(flowCode, request, cancellationToken);
        return ApiOk(await service.ExecuteAsync(flowCode, request, cancellationToken));
    }
}

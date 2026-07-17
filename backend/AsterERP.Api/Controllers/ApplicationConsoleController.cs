using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-console")]
public sealed class ApplicationConsoleController(IApplicationConsoleService applicationConsoleService) : BaseApiController
{
    [HttpGet("summary")]
    [Permission(PermissionCodes.AppHomeView)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        return await ExecuteApplicationConsoleAsync(
            () => applicationConsoleService.GetSummaryAsync(cancellationToken));
    }

    [HttpGet("database-binding/status")]
    [Permission(PermissionCodes.AppHomeView)]
    public async Task<IActionResult> GetDatabaseBindingStatusAsync(CancellationToken cancellationToken)
    {
        return await ExecuteApplicationConsoleAsync(
            () => applicationConsoleService.GetDatabaseBindingStatusAsync(cancellationToken));
    }

    [HttpPost("database-binding/test")]
    [Permission(PermissionCodes.AppApplicationCenterView)]
    public async Task<IActionResult> TestDatabaseBindingAsync(
        [FromBody] ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteApplicationConsoleAsync(
            () => applicationConsoleService.TestDatabaseBindingAsync(request, cancellationToken));
    }

    [HttpPut("database-binding")]
    [Permission(PermissionCodes.AppApplicationCenterView)]
    public async Task<IActionResult> SaveDatabaseBindingAsync(
        [FromBody] ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteApplicationConsoleAsync(
            () => applicationConsoleService.SaveDatabaseBindingAsync(request, cancellationToken));
    }

    private async Task<IActionResult> ExecuteApplicationConsoleAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            return ApiOk(await action());
        }
        catch (ValidationException ex) when (ex.Code == ErrorCodes.PermissionDenied)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResultFactory.Fail<object?>(ex.Message, HttpContext.TraceIdentifier, ex.Code));
        }
    }
}

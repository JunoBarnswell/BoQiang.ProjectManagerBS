using AsterERP.Api.Application.Auth;
using AsterERP.Api.Infrastructure;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Contracts.Auth;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Volo.Abp.Users;

namespace AsterERP.Api.Controllers;

[Route("api/application-auth/tenants/{tenantId}/apps/{appCode}")]
public sealed class ApplicationAuthController(
    IApplicationAuthService applicationAuthService,
    ApplicationLoginBootstrapCache bootstrapCache,
    ICurrentUser currentUser) : BaseApiController
{
    [HttpGet("bootstrap")]
    public async Task<IActionResult> BootstrapAsync(
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var canManageInitialBinding =
            currentUser.IsAsterErpAuthenticated() && currentUser.IsAsterErpPlatformAdmin();
        var response = await bootstrapCache.GetOrCreateAsync(
            tenantId,
            appCode,
            canManageInitialBinding,
            async sharedToken => RedactBootstrapDiagnostics(
                await applicationAuthService.GetBootstrapAsync(
                    tenantId,
                    appCode,
                    sharedToken)),
            cancellationToken);
        return ApiOk(response);
    }

    [HttpPost("database-binding/test")]
    public async Task<IActionResult> TestDatabaseBindingAsync(
        string tenantId,
        string appCode,
        [FromBody] ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => applicationAuthService.TestInitialDatabaseBindingAsync(tenantId, appCode, request, cancellationToken));
    }

    [HttpPut("database-binding")]
    public async Task<IActionResult> SaveDatabaseBindingAsync(
        string tenantId,
        string appCode,
        [FromBody] ApplicationDatabaseBindingRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var response = await applicationAuthService.SaveInitialDatabaseBindingAsync(
                tenantId,
                appCode,
                request,
                cancellationToken);
            bootstrapCache.Remove(tenantId, appCode);
            return response;
        });
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthenticationRateLimitPolicy.Name)]
    public async Task<IActionResult> LoginAsync(
        string tenantId,
        string appCode,
        [FromBody] ApplicationLoginRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => applicationAuthService.LoginAsync(tenantId, appCode, request, HttpContext, cancellationToken));
    }

    internal static ApplicationLoginBootstrapResponse RedactBootstrapDiagnostics(
        ApplicationLoginBootstrapResponse response) =>
        response with
        {
            DatabaseBinding = response.DatabaseBinding with
            {
                Provider = null,
                DisplayName = null,
                DatabaseName = null,
                UpdatedAt = null,
                Message = null
            }
        };

    private async Task<IActionResult> ExecuteAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            return ApiOk(await action());
        }
        catch (ValidationException ex) when (ex.Code == ErrorCodes.AuthenticationRequired)
        {
            return Unauthorized(ApiResultFactory.Fail<object?>(ex.Message, HttpContext.TraceIdentifier, ex.Code));
        }
        catch (ValidationException ex) when (ex.Code == ErrorCodes.PermissionDenied)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResultFactory.Fail<object?>(ex.Message, HttpContext.TraceIdentifier, ex.Code));
        }
    }
}

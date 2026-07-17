using AsterERP.Api.Application.Auth;
using AsterERP.Api.Infrastructure;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AsterERP.Api.Infrastructure.Security;

namespace AsterERP.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IAuthSessionService authSessionService) : BaseApiController
{
    [HttpPost("login")]
    [EnableRateLimiting(AuthenticationRateLimitPolicy.Name)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, HttpContext, cancellationToken);
        return ApiOk(response);
    }

    [HttpPost("initial-admin-password-recovery")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthenticationRateLimitPolicy.Name)]
    public async Task<IActionResult> RecoverInitialAdminPasswordAsync(
        [FromBody] InitialAdminPasswordRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        await authService.RecoverInitialAdminPasswordAsync(request, HttpContext, cancellationToken);
        return ApiOk(true);
    }

    [HttpGet("me")]
    public async Task<IActionResult> MeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await authService.GetSessionAsync(cancellationToken);
            return ApiOk(response);
        }
        catch (ValidationException ex) when (ex.Code == ErrorCodes.AuthenticationRequired)
        {
            return Unauthorized(ApiResultFactory.Fail<object?>(ex.Message, HttpContext.TraceIdentifier, ex.Code));
        }
    }

    [HttpGet("workspaces")]
    public async Task<IActionResult> WorkspacesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await authService.GetWorkspacesAsync(cancellationToken));
        }
        catch (ValidationException ex) when (ex.Code == ErrorCodes.AuthenticationRequired)
        {
            return Unauthorized(ApiResultFactory.Fail<object?>(ex.Message, HttpContext.TraceIdentifier, ex.Code));
        }
    }

    [HttpPost("switch-workspace")]
    public async Task<IActionResult> SwitchWorkspaceAsync([FromBody] SwitchWorkspaceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await authService.SwitchWorkspaceAsync(request, HttpContext, cancellationToken));
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

    [HttpPost("switch-platform")]
    public async Task<IActionResult> SwitchPlatformAsync([FromBody] SwitchPlatformRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await authService.SwitchPlatformAsync(request ?? new SwitchPlatformRequest(), HttpContext, cancellationToken));
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

    [HttpGet("current-workspace")]
    public async Task<IActionResult> CurrentWorkspaceAsync(CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await authService.GetCurrentWorkspaceAsync(cancellationToken));
        }
        catch (ValidationException ex) when (ex.Code == ErrorCodes.AuthenticationRequired)
        {
            return Unauthorized(ApiResultFactory.Fail<object?>(ex.Message, HttpContext.TraceIdentifier, ex.Code));
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = await authSessionService.RefreshCurrentSessionAsync(HttpContext, cancellationToken);
            return ApiOk(new { AccessToken = accessToken });
        }
        catch (ValidationException ex) when (ex.Code == ErrorCodes.AuthenticationRequired)
        {
            return Unauthorized(ApiResultFactory.Fail<object?>(ex.Message, HttpContext.TraceIdentifier, ex.Code));
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await authSessionService.RevokeCurrentSessionAsync(HttpContext, cancellationToken);
        return ApiOk(true);
    }
}

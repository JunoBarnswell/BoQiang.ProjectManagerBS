using AsterERP.Api.Application.Auth;
using AsterERP.Api.Application.Platform;
using AsterERP.Api.Infrastructure.Logging;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.System.Logs;
using AsterERP.Contracts.Platform;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.Platform.Applications;

public sealed class PlatformApplicationEntryService(
    ICurrentUser currentUser,
    IWorkspaceTransitionService workspaceTransitionService,
    PlatformAccessGuard accessGuard,
    IOperationLogWriter operationLogWriter) : IPlatformApplicationEntryService
{
    public async Task<ApplicationBackendEntryResponse> EnterAsync(
        string appCode,
        ApplicationBackendEntryRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        EnsureRequest(appCode, request);
        EnsureAuthenticated();
        accessGuard.EnsurePlatformAdmin();

        try
        {
            var user = await workspaceTransitionService.ResolveCurrentUserAsync(currentUser.GetAsterErpUserId(), cancellationToken);
            var snapshot = await workspaceTransitionService.EnterApplicationBackendAsync(
                user,
                request.TenantId,
                appCode,
                httpContext.Request.Headers.Authorization.ToString(),
                cancellationToken);

            await WriteOperationLogAsync(
                appCode,
                request,
                httpContext,
                true,
                StatusCodes.Status200OK,
                null,
                cancellationToken);

            return new ApplicationBackendEntryResponse(
                snapshot.CurrentWorkspace,
                snapshot.User,
                snapshot.Menus,
                snapshot.PermissionCodes,
                snapshot.Branding,
                snapshot.DefaultRoutePath);
        }
        catch (Exception ex)
        {
            await WriteOperationLogAsync(
                appCode,
                request,
                httpContext,
                false,
                ResolveStatusCode(ex),
                ex.Message,
                cancellationToken);
            throw;
        }
    }

    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }
    }

    private static void EnsureRequest(string appCode, ApplicationBackendEntryRequest request)
    {
        if (string.IsNullOrWhiteSpace(appCode))
        {
            throw new ValidationException("应用编码不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            throw new ValidationException("租户不能为空");
        }
    }

    private Task WriteOperationLogAsync(
        string appCode,
        ApplicationBackendEntryRequest request,
        HttpContext httpContext,
        bool isSuccess,
        int statusCode,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.IsAsterErpAuthenticated() ? currentUser.GetAsterErpUserId() : null;
        var userName = currentUser.IsAsterErpAuthenticated() ? currentUser.UserName : null;
        var operationLog = new SystemOperationLogEntity
        {
            TraceId = httpContext.TraceIdentifier,
            RequestPath = $"/api/platform/applications/{appCode.Trim().ToUpperInvariant()}/enter",
            RequestMethod = "POST",
            ModuleName = "Platform",
            OperationType = "ApplicationBackendEntry",
            ActionName = "EnterApplicationBackend",
            RequestQuery = $"tenantId={request.TenantId};source={request.Source}",
            ClientIp = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserId = userId,
            UserName = userName,
            StatusCode = statusCode,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            CreatedBy = userId
        };

        return operationLogWriter.WriteAsync(operationLog, cancellationToken);
    }

    private static int ResolveStatusCode(Exception exception)
    {
        return exception is ValidationException validationException
            ? validationException.Code switch
            {
                ErrorCodes.AuthenticationRequired => StatusCodes.Status401Unauthorized,
                ErrorCodes.PermissionDenied => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            }
            : StatusCodes.Status500InternalServerError;
    }
}

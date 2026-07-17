using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.AsterScene;

public sealed class AsterSceneWorkspaceContext(ICurrentUser currentUser)
{
    public AsterSceneWorkspace Resolve()
    {
        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new ValidationException("Please sign in first.", ErrorCodes.AuthenticationRequired);
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            throw new ValidationException("Please select a tenant application workspace.", ErrorCodes.PermissionDenied);
        }

        return new AsterSceneWorkspace(
            tenantId.Trim(),
            appCode.Trim().ToUpperInvariant(),
            currentUser.GetAsterErpUserId());
    }
}

public sealed record AsterSceneWorkspace(string TenantId, string AppCode, string UserId);

using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataCenterWorkspaceResolver(ICurrentUser currentUser)
{
    public ApplicationDataCenterWorkspace Resolve()
    {
        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant();
        var userId = currentUser.GetAsterErpUserId();
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(appCode) ||
            string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("请先进入应用工作区", ErrorCodes.PermissionDenied);
        }

        return new ApplicationDataCenterWorkspace(tenantId, appCode, userId);
    }
}

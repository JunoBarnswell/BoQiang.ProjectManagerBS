using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Tenant;

public sealed class TenantAccessGuard(ICurrentUser currentUser)
{
    public string GetTenantIdForTenantAdmin()
    {
        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ValidationException("请先选择租户工作区", ErrorCodes.PermissionDenied);
        }

        if (!currentUser.IsAsterErpTenantAdmin() && !currentUser.HasAsterErpPermission("*"))
        {
            throw new ValidationException("仅租户超级管理员可访问租户管理功能", ErrorCodes.PermissionDenied);
        }

        return tenantId;
    }
}

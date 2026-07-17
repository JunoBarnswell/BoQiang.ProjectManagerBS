using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.Ai;

public sealed class AiWorkspaceContext(ICurrentUser currentUser)
{
    public AiWorkspace Resolve()
    {
        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            throw new ValidationException("请先选择租户应用工作区", ErrorCodes.PermissionDenied);
        }

        return new AiWorkspace(tenantId, appCode.Trim().ToUpperInvariant(), currentUser.GetAsterErpUserId());
    }
}

public sealed record AiWorkspace(string TenantId, string AppCode, string UserId);

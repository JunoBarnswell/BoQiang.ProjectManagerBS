using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Platform;

public sealed class PlatformAccessGuard(ICurrentUser currentUser)
{
    public void EnsurePlatformAdmin()
    {
        if (!currentUser.IsAsterErpAuthenticated() ||
            !currentUser.IsAsterErpPlatformAdmin() ||
            !string.Equals(currentUser.GetAsterErpAppCode(), "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("仅平台管理员可访问平台管理功能", ErrorCodes.PermissionDenied);
        }
    }
}

using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// Compatibility scope for platform-level project-management records.
/// AppCode remains present in the existing schema and entities, but it is no
/// longer derived from the selected application workspace.
/// </summary>
public static class ProjectManagementPlatformScope
{
    public const string AppCode = "SYSTEM";

    public static void RequireSystemWorkspace(ICurrentUser currentUser)
    {
        if (!string.Equals(currentUser.GetAsterErpAppCode(), AppCode, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("项目管理仅可从平台 SYSTEM 工作区访问", ErrorCodes.PermissionDenied);
    }
}

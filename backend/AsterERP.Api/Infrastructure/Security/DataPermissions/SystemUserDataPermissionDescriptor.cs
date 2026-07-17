using System.Linq.Expressions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Shared;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class SystemUserDataPermissionDescriptor(
    ICurrentUser currentUser,
    IDataScopeDepartmentResolver departmentResolver)
    : IDataPermissionDescriptor<SystemUserEntity>
{
    public async Task<Expression<Func<SystemUserEntity, bool>>?> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentUser.IsAsterErpAuthenticated() ||
            currentUser.HasAsterErpPermission("*") ||
            string.Equals(currentUser.GetAsterErpDataScope(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var userId = currentUser.GetAsterErpUserId();
        var dataScope = currentUser.GetAsterErpDataScope();
        if (string.Equals(dataScope, "SELF", StringComparison.OrdinalIgnoreCase))
        {
            return item => item.Id == userId;
        }

        var deptId = currentUser.GetAsterErpDeptId();
        if (string.IsNullOrWhiteSpace(deptId))
        {
            return item => item.Id == userId;
        }

        if (string.Equals(dataScope, "DEPT", StringComparison.OrdinalIgnoreCase))
        {
            return item => item.DeptId == deptId;
        }

        if (string.Equals(dataScope, "DEPT_AND_CHILD", StringComparison.OrdinalIgnoreCase))
        {
            var deptIds = await departmentResolver.ResolveDepartmentAndChildrenAsync(deptId, cancellationToken);
            return item => item.DeptId != null && deptIds.Contains(item.DeptId);
        }

        return item => item.Id == userId;
    }
}

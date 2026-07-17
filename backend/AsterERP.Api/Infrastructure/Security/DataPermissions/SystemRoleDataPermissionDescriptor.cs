using System.Linq.Expressions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Shared;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class SystemRoleDataPermissionDescriptor(ICurrentUser currentUser)
    : IDataPermissionDescriptor<SystemRoleEntity>
{
    public Task<Expression<Func<SystemRoleEntity, bool>>?> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentUser.IsAsterErpAuthenticated() || currentUser.HasAsterErpPermission("*"))
        {
            return Task.FromResult<Expression<Func<SystemRoleEntity, bool>>?>(null);
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            return Task.FromResult<Expression<Func<SystemRoleEntity, bool>>?>(item => false);
        }

        tenantId = tenantId.Trim();
        appCode = appCode.Trim().ToUpperInvariant();
        Expression<Func<SystemRoleEntity, bool>> predicate = item =>
            item.TenantId == tenantId && item.AppCode == appCode;
        return Task.FromResult<Expression<Func<SystemRoleEntity, bool>>?>(predicate);
    }
}

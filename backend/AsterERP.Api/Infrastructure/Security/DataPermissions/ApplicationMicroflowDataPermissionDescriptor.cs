using System.Linq.Expressions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class ApplicationMicroflowDataPermissionDescriptor(ICurrentUser currentUser)
    : IDataPermissionDescriptor<ApplicationMicroflowEntity>
{
    public Task<Expression<Func<ApplicationMicroflowEntity, bool>>?> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentUser.IsAsterErpAuthenticated() || currentUser.HasAsterErpPermission("*"))
        {
            return Task.FromResult<Expression<Func<ApplicationMicroflowEntity, bool>>?>(null);
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            return Task.FromResult<Expression<Func<ApplicationMicroflowEntity, bool>>?>(item => false);
        }

        tenantId = tenantId.Trim();
        appCode = appCode.Trim().ToUpperInvariant();
        Expression<Func<ApplicationMicroflowEntity, bool>> predicate = item =>
            item.TenantId == tenantId && item.AppCode == appCode;
        return Task.FromResult<Expression<Func<ApplicationMicroflowEntity, bool>>?>(predicate);
    }
}

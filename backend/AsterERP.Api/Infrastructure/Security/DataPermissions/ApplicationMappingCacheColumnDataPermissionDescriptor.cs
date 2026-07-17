using System.Linq.Expressions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class ApplicationMappingCacheColumnDataPermissionDescriptor(ICurrentUser currentUser)
    : IDataPermissionDescriptor<ApplicationMappingCacheColumnEntity>
{
    public Task<Expression<Func<ApplicationMappingCacheColumnEntity, bool>>?> BuildAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentUser.IsAsterErpAuthenticated() || currentUser.HasAsterErpPermission("*"))
            return Task.FromResult<Expression<Func<ApplicationMappingCacheColumnEntity, bool>>?>(null);

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
            return Task.FromResult<Expression<Func<ApplicationMappingCacheColumnEntity, bool>>?>(item => false);

        tenantId = tenantId.Trim();
        appCode = appCode.Trim().ToUpperInvariant();
        Expression<Func<ApplicationMappingCacheColumnEntity, bool>> predicate = item => item.TenantId == tenantId && item.AppCode == appCode;
        return Task.FromResult<Expression<Func<ApplicationMappingCacheColumnEntity, bool>>?>(predicate);
    }
}

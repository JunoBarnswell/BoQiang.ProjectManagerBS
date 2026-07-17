using System.Linq.Expressions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class ApplicationMappingCacheParameterDataPermissionDescriptor(ICurrentUser currentUser)
    : IDataPermissionDescriptor<ApplicationMappingCacheParameterEntity>
{
    public Task<Expression<Func<ApplicationMappingCacheParameterEntity, bool>>?> BuildAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentUser.IsAsterErpAuthenticated() || currentUser.HasAsterErpPermission("*"))
            return Task.FromResult<Expression<Func<ApplicationMappingCacheParameterEntity, bool>>?>(null);

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
            return Task.FromResult<Expression<Func<ApplicationMappingCacheParameterEntity, bool>>?>(item => false);

        tenantId = tenantId.Trim();
        appCode = appCode.Trim().ToUpperInvariant();
        Expression<Func<ApplicationMappingCacheParameterEntity, bool>> predicate = item => item.TenantId == tenantId && item.AppCode == appCode;
        return Task.FromResult<Expression<Func<ApplicationMappingCacheParameterEntity, bool>>?>(predicate);
    }
}

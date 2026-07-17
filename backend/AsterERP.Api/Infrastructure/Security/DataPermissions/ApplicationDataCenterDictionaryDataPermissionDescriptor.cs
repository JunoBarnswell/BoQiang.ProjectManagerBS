using System.Linq.Expressions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class ApplicationDataCenterDictionaryDataPermissionDescriptor(ICurrentUser currentUser)
    : IDataPermissionDescriptor<ApplicationDataCenterDictionaryEntity>
{
    public Task<Expression<Func<ApplicationDataCenterDictionaryEntity, bool>>?> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildFilter());
    }

    private Expression<Func<ApplicationDataCenterDictionaryEntity, bool>>? BuildFilter()
    {
        if (!currentUser.IsAsterErpAuthenticated() || currentUser.HasAsterErpPermission("*"))
        {
            return null;
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            return item => false;
        }

        tenantId = tenantId.Trim();
        appCode = appCode.Trim().ToUpperInvariant();
        return item => item.TenantId == tenantId && item.AppCode == appCode;
    }
}

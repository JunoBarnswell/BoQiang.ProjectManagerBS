using System.Linq.Expressions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class ApplicationDataMutationLedgerDataPermissionDescriptor(ICurrentUser currentUser)
    : IDataPermissionDescriptor<ApplicationDataMutationLedgerEntity>
{
    public Task<Expression<Func<ApplicationDataMutationLedgerEntity, bool>>?> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentUser.IsAsterErpAuthenticated() || currentUser.HasAsterErpPermission("*"))
        {
            return Task.FromResult<Expression<Func<ApplicationDataMutationLedgerEntity, bool>>?>(null);
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            return Task.FromResult<Expression<Func<ApplicationDataMutationLedgerEntity, bool>>?>(item => false);
        }

        tenantId = tenantId.Trim();
        appCode = appCode.Trim().ToUpperInvariant();
        Expression<Func<ApplicationDataMutationLedgerEntity, bool>> predicate = item =>
            item.TenantId == tenantId && item.AppCode == appCode;
        return Task.FromResult<Expression<Func<ApplicationDataMutationLedgerEntity, bool>>?>(predicate);
    }
}

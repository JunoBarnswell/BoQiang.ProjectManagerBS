using System.Linq.Expressions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class ApplicationDataSourceSqlitePathApprovalDataPermissionDescriptor(ICurrentUser currentUser)
    : IDataPermissionDescriptor<ApplicationDataSourceSqlitePathApprovalEntity>
{
    public Task<Expression<Func<ApplicationDataSourceSqlitePathApprovalEntity, bool>>?> BuildAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!currentUser.IsAsterErpAuthenticated() || currentUser.HasAsterErpPermission("*"))
            return Task.FromResult<Expression<Func<ApplicationDataSourceSqlitePathApprovalEntity, bool>>?>(null);

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
            return Task.FromResult<Expression<Func<ApplicationDataSourceSqlitePathApprovalEntity, bool>>?>(item => false);

        tenantId = tenantId.Trim();
        appCode = appCode.Trim().ToUpperInvariant();
        Expression<Func<ApplicationDataSourceSqlitePathApprovalEntity, bool>> predicate = item =>
            item.TenantId == tenantId && item.AppCode == appCode;
        return Task.FromResult<Expression<Func<ApplicationDataSourceSqlitePathApprovalEntity, bool>>?>(predicate);
    }
}

using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Infrastructure.Database;

public sealed class WorkspaceDatabaseAccessor(
    ISqlSugarClient mainDb,
    ICurrentUser currentUser,
    ApplicationDatabaseBindingResolver bindingResolver,
    IApplicationDatabaseConnectionFactory connectionFactory,
    ILogger<WorkspaceDatabaseAccessor> logger) : IWorkspaceDatabaseAccessor, IDisposable
{
    private ISqlSugarClient? applicationDb;
    private bool applicationDbResolved;

    public ISqlSugarClient MainDb => mainDb;

    public ISqlSugarClient GetCurrentDb()
    {
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(appCode) ||
            string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            return mainDb;
        }

        return RequireApplicationDb();
    }

    public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetCurrentDb());
    }

    public ISqlSugarClient RequireApplicationDb()
    {
        if (applicationDbResolved)
        {
            return applicationDb ?? throw new ValidationException("请先绑定应用数据库", ErrorCodes.ApplicationDatabaseNotBound);
        }

        applicationDbResolved = true;
        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(appCode) ||
            string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("请先进入应用工作区", ErrorCodes.PermissionDenied);
        }

        var tenantApp = mainDb.Queryable<SystemTenantAppEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .First();
        if (tenantApp is null)
        {
            throw new ValidationException("当前应用工作区不存在或已停用", ErrorCodes.PermissionDenied);
        }

        var binding = bindingResolver.Resolve(tenantApp.ConfigJson, tenantId, appCode);
        if (binding is null)
        {
            throw new ValidationException("请先绑定应用数据库", ErrorCodes.ApplicationDatabaseNotBound);
        }

        try
        {
            applicationDb = connectionFactory.Create(binding);
            applicationDb.Ado.GetInt("SELECT 1");
            return applicationDb;
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Application database connection failed for {TenantId}/{AppCode}", tenantId, appCode);
            throw new ValidationException("应用数据库连接失败，请检查数据库绑定", ErrorCodes.ApplicationDatabaseConnectionFailed);
        }
    }

    public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RequireApplicationDb());
    }

    public void Dispose()
    {
        if (applicationDb is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

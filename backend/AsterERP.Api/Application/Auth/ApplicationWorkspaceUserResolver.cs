using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Auth;

public sealed class ApplicationWorkspaceUserResolver(
    ApplicationDatabaseBindingResolver bindingResolver,
    IApplicationDatabaseConnectionFactory connectionFactory,
    ILogger<ApplicationWorkspaceUserResolver> logger)
{
    public async Task<SystemUserEntity?> FindByIdAsync(
        string userId,
        string? tenantAppConfigJson,
        string? tenantId = null,
        string? appCode = null,
        CancellationToken cancellationToken = default)
    {
        var binding = ResolveRequiredBinding(tenantAppConfigJson, tenantId, appCode);
        return await WithApplicationDbAsync(
            binding,
            async appDb => (await appDb.Queryable<SystemUserEntity>()
                .Where(item => item.Id == userId && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault());
    }

    public async Task<SystemUserEntity?> FindByUserNameAsync(
        string userName,
        string? tenantAppConfigJson,
        string? tenantId = null,
        string? appCode = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserName = userName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return null;
        }

        var binding = ResolveRequiredBinding(tenantAppConfigJson, tenantId, appCode);
        return await WithApplicationDbAsync(
            binding,
            async appDb => (await appDb.Queryable<SystemUserEntity>()
                .Where(item => item.UserName == normalizedUserName && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault());
    }

    public async Task UpdatePasswordHashAsync(
        string userId,
        string passwordHash,
        string? tenantAppConfigJson,
        string? tenantId = null,
        string? appCode = null,
        CancellationToken cancellationToken = default)
    {
        var binding = ResolveRequiredBinding(tenantAppConfigJson, tenantId, appCode);
        await WithApplicationDbAsync(
            binding,
            async appDb =>
            {
                await appDb.Updateable<SystemUserEntity>()
                    .SetColumns(item => item.PasswordHash == passwordHash)
                    .SetColumns(item => item.PasswordResetRequired == false)
                    .SetColumns(item => item.PasswordFormatVersion == "v1")
                    .SetColumns(item => item.UpdatedTime == DateTime.UtcNow)
                    .Where(item => item.Id == userId && !item.IsDeleted)
                    .ExecuteCommandAsync(cancellationToken);
                return true;
            });
    }

    public async Task MarkPasswordResetRequiredAsync(
        string userId,
        string format,
        string? tenantAppConfigJson,
        string? tenantId = null,
        string? appCode = null,
        CancellationToken cancellationToken = default)
    {
        var binding = ResolveRequiredBinding(tenantAppConfigJson, tenantId, appCode);
        await WithApplicationDbAsync(
            binding,
            async appDb =>
            {
                await appDb.Updateable<SystemUserEntity>()
                    .SetColumns(item => item.PasswordResetRequired == true)
                    .SetColumns(item => item.PasswordFormatVersion == format)
                    .SetColumns(item => item.UpdatedTime == DateTime.UtcNow)
                    .Where(item => item.Id == userId && !item.IsDeleted)
                    .ExecuteCommandAsync(cancellationToken);
                return true;
            });
    }

    private ApplicationDatabaseBindingOptions ResolveRequiredBinding(
        string? tenantAppConfigJson,
        string? tenantId,
        string? appCode)
    {
        var binding = bindingResolver.Resolve(tenantAppConfigJson, tenantId, appCode);
        return binding ?? throw new ValidationException("请先由平台管理员绑定应用数据库", ErrorCodes.ApplicationDatabaseNotBound);
    }

    private async Task<T> WithApplicationDbAsync<T>(
        ApplicationDatabaseBindingOptions binding,
        Func<ISqlSugarClient, Task<T>> action)
    {
        ISqlSugarClient? appDb = null;
        try
        {
            appDb = connectionFactory.Create(binding);
            return await action(appDb);
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            logger.LogWarning(ex, "Application database user resolution failed for provider {Provider}", binding.Provider);
            throw new ValidationException("应用数据库连接失败，请检查数据库绑定", ErrorCodes.ApplicationDatabaseConnectionFailed);
        }
        finally
        {
            if (appDb is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationDatabasePermissionReader(
    ApplicationDatabaseBindingResolver bindingResolver,
    IApplicationDatabaseConnectionFactory connectionFactory,
    ApplicationDatabaseSchemaInitializer schemaInitializer,
    ILogger<ApplicationDatabasePermissionReader> logger)
{
    public async Task<IReadOnlyList<string>> ReadPermissionCodesAsync(
        SystemUserEntity user,
        string tenantId,
        string appCode,
        string? tenantAppConfigJson,
        CancellationToken cancellationToken = default)
    {
        var binding = bindingResolver.Resolve(tenantAppConfigJson, tenantId, appCode);
        if (binding is null)
        {
            return [];
        }

        try
        {
            using var appDb = CreateDisposableClient(binding);
            await schemaInitializer.EnsureBaselineAsync(appDb.Client, tenantId, appCode, user, cancellationToken, tenantAppConfigJson);
            return user.IsAdmin
                ? await ReadAllEnabledPermissionCodesAsync(appDb.Client, cancellationToken)
                : await ReadRolePermissionCodesAsync(appDb.Client, user.Id, tenantId, appCode, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read application database permissions for {TenantId}/{AppCode}", tenantId, appCode);
            return [];
        }
    }

    public async Task<IReadOnlyList<SystemRoleEntity>> ReadRolesAsync(
        SystemUserEntity user,
        string tenantId,
        string appCode,
        string? tenantAppConfigJson,
        CancellationToken cancellationToken = default)
    {
        var binding = bindingResolver.Resolve(tenantAppConfigJson, tenantId, appCode);
        if (binding is null)
        {
            return [];
        }

        try
        {
            using var appDb = CreateDisposableClient(binding);
            await schemaInitializer.EnsureBaselineAsync(appDb.Client, tenantId, appCode, user, cancellationToken, tenantAppConfigJson);
            if (user.IsAdmin)
            {
                return await appDb.Client.Queryable<SystemRoleEntity>()
                    .Where(item => !item.IsDeleted && item.IsEnabled)
                    .ToListAsync(cancellationToken);
            }

            var roleIds = await ReadUserRoleIdsAsync(appDb.Client, user.Id, cancellationToken);
            if (roleIds.Count == 0)
            {
                return [];
            }

            return await appDb.Client.Queryable<SystemRoleEntity>()
                .Where(item => roleIds.Contains(item.Id) && !item.IsDeleted && item.IsEnabled)
                .ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read application database roles for {TenantId}/{AppCode}", tenantId, appCode);
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> ReadAllEnabledPermissionCodesAsync(
        ISqlSugarClient appDb,
        CancellationToken cancellationToken)
    {
        return (await appDb.Queryable<SystemPermissionCodeEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled)
            .Select(item => item.PermissionCode)
            .ToListAsync(cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> ReadRolePermissionCodesAsync(
        ISqlSugarClient appDb,
        string userId,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var roleIds = await ReadUserRoleIdsAsync(appDb, userId, cancellationToken);
        if (roleIds.Count == 0)
        {
            return [];
        }

        var enabledRoleIds = await appDb.Queryable<SystemRoleEntity>()
            .Where(item => roleIds.Contains(item.Id) && !item.IsDeleted && item.IsEnabled)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (enabledRoleIds.Count == 0)
        {
            return [];
        }

        var permissionCodeIds = await appDb.Queryable<SystemRolePermissionEntity>()
            .Where(item => enabledRoleIds.Contains(item.RoleId) && !item.IsDeleted)
            .Select(item => item.PermissionCodeId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (permissionCodeIds.Count == 0)
        {
            return [];
        }

        return (await appDb.Queryable<SystemPermissionCodeEntity>()
            .Where(item => permissionCodeIds.Contains(item.Id) && !item.IsDeleted && item.IsEnabled)
            .Select(item => item.PermissionCode)
            .ToListAsync(cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<List<string>> ReadUserRoleIdsAsync(
        ISqlSugarClient appDb,
        string userId,
        CancellationToken cancellationToken)
    {
        return await appDb.Queryable<SystemUserRoleEntity>()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .Select(item => item.RoleId)
            .ToListAsync(cancellationToken);
    }

    private DisposableApplicationDb CreateDisposableClient(ApplicationDatabaseBindingOptions binding)
    {
        var client = connectionFactory.Create(binding);
        return new DisposableApplicationDb(client);
    }

    private sealed class DisposableApplicationDb(ISqlSugarClient client) : IDisposable
    {
        public ISqlSugarClient Client { get; } = client;

        public void Dispose()
        {
            if (Client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

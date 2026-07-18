using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using System.Collections.Concurrent;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationDatabaseSchemaInitializer(
    ApplicationSystemAdministrationSchemaInitializer systemAdministrationSchemaInitializer,
    ApplicationDataCenterApplicationDatabaseSchemaInitializer dataCenterSchemaInitializer,
    ProjectManagementSchemaMigrator projectManagementSchemaMigrator,
    ApplicationWorkflowSchemaInitializer workflowSchemaInitializer,
    ApplicationDatabaseBaselineSeeder baselineSeeder,
    ILogger<ApplicationDatabaseSchemaInitializer> logger)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> BaselineLocks = new(StringComparer.OrdinalIgnoreCase);

    public async Task InitializeAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        SystemUserEntity currentUser,
        CancellationToken cancellationToken = default,
        string? tenantAppConfigJson = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var baselineLock = GetBaselineLock(tenantId, appCode);
        await baselineLock.WaitAsync(cancellationToken);
        try
        {
            systemAdministrationSchemaInitializer.Initialize(appDb);
            dataCenterSchemaInitializer.Initialize(appDb);
            await projectManagementSchemaMigrator.MigrateAsync(appDb, cancellationToken);
            await workflowSchemaInitializer.InitializeAsync(appDb, tenantId, appCode, currentUser.Id, cancellationToken);
            await baselineSeeder.SeedAsync(appDb, tenantId, appCode, currentUser, cancellationToken, tenantAppConfigJson);
            logger.LogInformation("Application database schema initialized for {TenantId}/{AppCode}", tenantId, appCode);
        }
        finally
        {
            baselineLock.Release();
        }
    }

    public async Task EnsureBaselineAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        SystemUserEntity currentUser,
        CancellationToken cancellationToken = default,
        string? tenantAppConfigJson = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var baselineLock = GetBaselineLock(tenantId, appCode);
        await baselineLock.WaitAsync(cancellationToken);
        try
        {
            systemAdministrationSchemaInitializer.Initialize(appDb);
            dataCenterSchemaInitializer.Initialize(appDb);
            await projectManagementSchemaMigrator.MigrateAsync(appDb, cancellationToken);
            if (await baselineSeeder.IsCurrentAsync(appDb, tenantId, appCode, tenantAppConfigJson, cancellationToken))
            {
                return;
            }

            await workflowSchemaInitializer.InitializeAsync(appDb, tenantId, appCode, currentUser.Id, cancellationToken);
            await baselineSeeder.SeedAsync(appDb, tenantId, appCode, currentUser, cancellationToken, tenantAppConfigJson);
            logger.LogInformation("Application database baseline repaired for {TenantId}/{AppCode}", tenantId, appCode);
        }
        finally
        {
            baselineLock.Release();
        }
    }

    private static SemaphoreSlim GetBaselineLock(string tenantId, string appCode)
    {
        return BaselineLocks.GetOrAdd($"{tenantId}:{appCode}", static _ => new SemaphoreSlim(1, 1));
    }
}

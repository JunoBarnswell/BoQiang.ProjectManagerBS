using System.Collections.Concurrent;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Services;
using Volo.Abp.Users;

namespace AsterERP.Api.Infrastructure.Workflows;

public sealed class WorkflowWorkspaceRuntimeInitializer(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IWorkflowPersistenceStore workflowPersistenceStore,
    IProcessEngineConfiguration processEngineConfiguration,
    ApplicationWorkflowSchemaInitializer applicationWorkflowSchemaInitializer,
    ICurrentUser currentUser,
    ILogger<WorkflowWorkspaceRuntimeInitializer> logger)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> InitializationLocks = new(StringComparer.Ordinal);

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetCurrentDb();
        var storeKey = ResolveStoreKey(db.CurrentConnectionConfig.DbType.ToString(), db.CurrentConnectionConfig.ConnectionString);
        var initializationLock = InitializationLocks.GetOrAdd(storeKey, static _ => new SemaphoreSlim(1, 1));

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureApplicationWorkflowSchemaAsync(db, cancellationToken);
            await workflowPersistenceStore.InitializeAsync(processEngineConfiguration, cancellationToken);
            logger.LogDebug("Workflow runtime initialized for store {StoreKey}", storeKey);
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private async Task EnsureApplicationWorkflowSchemaAsync(
        SqlSugar.ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(appCode) ||
            string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase) ||
            ReferenceEquals(db, databaseAccessor.MainDb))
        {
            return;
        }

        await applicationWorkflowSchemaInitializer.InitializeAsync(
            db,
            tenantId,
            appCode,
            currentUser.GetAsterErpUserId(),
            cancellationToken);
    }

    private static string ResolveStoreKey(string dbType, string connectionString)
    {
        return $"{dbType}:{connectionString}";
    }
}

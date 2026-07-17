using AsterERP.Api.Infrastructure.Workflows;
using AsterERP.Api.Modules.System.Parameters;
using AsterERP.Workflow.Persistence.Database;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationWorkflowSchemaInitializer(
    WorkflowApprovalSchemaInitializer workflowApprovalSchemaInitializer,
    WorkflowIdentitySyncService workflowIdentitySyncService,
    ILogger<ApplicationWorkflowSchemaInitializer> logger)
{
    public const string SchemaVersion = "2026.07.05.1";
    public const string SchemaParameterKey = "app.workflow.schemaVersion";

    public async Task InitializeAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await IsCurrentAsync(appDb, cancellationToken) && RequiredTablesExist(appDb))
        {
            return;
        }

        new DatabaseInitializer(appDb, new SqliteSchemaValidator(appDb)).Initialize();
        workflowApprovalSchemaInitializer.Initialize(appDb);
        await workflowIdentitySyncService.SyncAsync(appDb, cancellationToken);
        await UpsertSchemaVersionAsync(appDb, currentUserId, cancellationToken);

        logger.LogInformation(
            "Application workflow schema initialized for {TenantId}/{AppCode}",
            tenantId,
            appCode);
    }

    private static async Task<bool> IsCurrentAsync(
        ISqlSugarClient appDb,
        CancellationToken cancellationToken)
    {
        var value = (await appDb.Queryable<SystemParameterEntity>()
            .Where(item => item.ParamKey == SchemaParameterKey && !item.IsDeleted && item.IsEnabled)
            .Select(item => item.ParamValue)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return string.Equals(value, SchemaVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiredTablesExist(ISqlSugarClient appDb)
    {
        return TableExists(appDb, "tbl_flow_model_info") &&
               TableExists(appDb, "ACT_RE_PROCDEF") &&
               TableExists(appDb, "workflow_bindings") &&
               TableExists(appDb, "ACT_ID_USER");
    }

    private static bool TableExists(ISqlSugarClient appDb, string tableName)
    {
        return appDb.DbMaintenance.IsAnyTable(tableName, false);
    }

    private static async Task UpsertSchemaVersionAsync(
        ISqlSugarClient appDb,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var entity = (await appDb.Queryable<SystemParameterEntity>()
            .Where(item => item.ParamKey == SchemaParameterKey)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (entity is null)
        {
            await appDb.Insertable(new SystemParameterEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                ParamName = "应用库 Workflow Schema 版本",
                ParamKey = SchemaParameterKey,
                ParamValue = SchemaVersion,
                Category = "application-workflow",
                IsEnabled = true,
                CreatedBy = currentUserId,
                CreatedTime = now,
                IsDeleted = false
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        entity.ParamName = "应用库 Workflow Schema 版本";
        entity.ParamValue = SchemaVersion;
        entity.Category = "application-workflow";
        entity.IsEnabled = true;
        entity.IsDeleted = false;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
        entity.UpdatedBy = currentUserId;
        entity.UpdatedTime = now;
        await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }
}

using AsterERP.Api.Infrastructure.Database;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

public sealed class AiCenterSchemaMigrator
{
    public Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schema = new SqliteSchemaExecutor(db);
        AiCenterCoreSchemaMigrator.Migrate(schema);
        AiCenterManagementSchemaMigrator.Migrate(schema);
        AiCenterFlowiseSchemaMigrator.Migrate(schema);
        AiCenterSchemaIndexMigrator.Migrate(schema);
        return Task.CompletedTask;
    }
}



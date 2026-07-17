using AsterERP.Api.Modules.ApplicationDataCenter;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationDataCenterApplicationDatabaseSchemaInitializer
{
    public void Initialize(ISqlSugarClient appDb)
    {
        appDb.CodeFirst.InitTables(
            typeof(ApplicationDataCenterPublishedSnapshot),
            typeof(ApplicationDataSourceCatalogSnapshotEntity),
            typeof(ApplicationDataSourceSchemaChangePlanEntity),
            typeof(ApplicationDataSourceSqlitePathApprovalEntity),
            typeof(ApplicationDataSourceSqlitePathApprovalAuditEntity),
            typeof(ApplicationSqlScriptAuditEntity),
            typeof(ApplicationDataMutationLedgerEntity),
            typeof(ApplicationMappingCacheEntity),
            typeof(ApplicationMappingCacheColumnEntity),
            typeof(ApplicationMappingCacheParameterEntity));
    }
}

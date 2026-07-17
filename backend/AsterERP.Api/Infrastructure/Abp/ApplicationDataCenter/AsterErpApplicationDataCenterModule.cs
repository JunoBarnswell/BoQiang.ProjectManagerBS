using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Abp.RuntimeCore;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;

[DependsOn(typeof(AsterErpRuntimeCoreModule))]
public sealed class AsterErpApplicationDataCenterModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        services.AddHttpClient(ApplicationDataOutboundHttpClient.Name, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(ApplicationDataOutboundHttpClient.CreatePrimaryHandler);
        services.AddScoped<ApplicationDataScopedHttpClientFactory>();
        services.AddScoped<ApplicationDataCenterWorkspaceResolver>();
        services.AddScoped<IApplicationDataSecretProtector, ApplicationDataSecretProtector>();
        services.AddScoped<ApplicationDataCenterRiskGuard>();
        services.AddScoped<ApplicationDataCenterTemplateCatalog>();
        services.AddScoped<ApplicationDataCenterPublishedSnapshotService>();
        services.AddScoped<ApplicationObjectReferenceService>();
        services.AddScoped<ApplicationDataCenterOverviewService>();
        services.AddScoped<ApplicationDataSourceWorkbenchService>();
        services.AddScoped<ApplicationDataSourceCatalogService>();
        services.AddScoped<ApplicationDataSourceSqlitePathApprovalService>();
        services.AddScoped<ApplicationDataSourceTableWorkbenchService>();
        services.AddScoped<ApplicationDataSourceTableRowService>();
        services.AddScoped<ApplicationDataSourceViewWorkbenchService>();
        services.AddScoped<ApplicationMappingCacheWorkbenchService>();
        services.AddScoped<ApplicationSystemAssignmentService>();
        services.AddScoped<ApplicationMicroflowExpressionReferenceValidator>();
        services.AddScoped<ApplicationMicroflowDefinitionValidator>();
        services.AddScoped<ApplicationMicroflowOutputSchemaSynchronizer>();
        services.AddScoped<ApplicationMicroflowContractService>();
        services.AddScoped<ApplicationMicroflowRuntimePermissionService>();
        services.AddScoped<ApplicationMicroflowPreviewResultBuilder>();
        services.AddScoped<ApplicationMicroflowRevisionService>();
        services.AddSingleton<RuntimeExpressionFunctionCatalog>();
        services.AddScoped<ApplicationDataCenterSqlScriptExpressionTokenizer>();
        services.AddScoped<ApplicationDataCenterSqlScriptExpressionParser>();
        services.AddScoped<ApplicationDataCenterSqlBuiltInVariableProvider>();
        services.AddScoped<ApplicationDataCenterSqlRbacFunctionEvaluator>();
        services.AddScoped<ApplicationDataCenterSqlScriptExpressionEvaluator>();
        services.AddScoped<ApplicationDataCenterSqlScriptFunctionParameterizer>();
        services.AddScoped<ApplicationDataCenterSqlScriptIfElseBlockReader>();
        services.AddScoped<ApplicationDataCenterSqlScriptParser>();
        services.AddScoped<ApplicationDataCenterSqlScriptValidator>();
        services.AddScoped<ApplicationDataCenterSqlScriptResultProjector>();
        services.AddScoped<ApplicationDataCenterSqlScriptAuditWriter>();
        services.AddScoped<ApplicationDataMutationLedgerService>();
        services.AddScoped<ApplicationDataCenterSqlScriptEngine>();
        services.AddScoped<ApplicationMicroflowService>();
        services.AddScoped<IApplicationMicroflowRuntimeService>(serviceProvider =>
            ActivatorUtilities.CreateInstance<ApplicationMicroflowRuntimeService>(
                serviceProvider,
                (IHttpClientFactory)serviceProvider.GetRequiredService<ApplicationDataScopedHttpClientFactory>()));
        services.AddScoped<ApplicationDataSourceConnectionFactory>();
        services.AddScoped<ApplicationDataSourceSqliteSandbox>();
        services.AddSingleton<IApplicationDataSourceProvider, SqliteApplicationDataSourceProvider>();
        services.AddSingleton<IApplicationDataSourceProvider, MySqlApplicationDataSourceProvider>();
        services.AddSingleton<IApplicationDataSourceProvider, PostgreSqlApplicationDataSourceProvider>();
        services.AddSingleton<IApplicationDataSourceProvider, SqlServerApplicationDataSourceProvider>();
        services.AddScoped<ApplicationDataSourceProviderRegistry>();
        services.AddScoped<ApplicationDataPreviewReader>();
        services.AddScoped<ApplicationDataSourceService>();
        services.AddScoped<ApplicationConnectionTestService>();
        services.AddScoped<ApplicationDataModelService>();
        services.AddScoped<ApplicationApiServiceService>();
        services.AddScoped<ApplicationEntityFieldService>();
        services.AddScoped<ApplicationDictionaryCodeService>();
        services.AddScoped<ApplicationQueryDatasetService>();
        services.AddScoped<ApplicationIntegrationTaskService>();
        services.AddScoped<ApplicationDataApiRuntimeService>(serviceProvider =>
            ActivatorUtilities.CreateInstance<ApplicationDataApiRuntimeService>(
                serviceProvider,
                (IHttpClientFactory)serviceProvider.GetRequiredService<ApplicationDataScopedHttpClientFactory>()));
        services.AddScoped<IDataModelProvider, ApplicationDataCenterSqlDataModelProvider>();
        services.AddScoped<IDataModelProvider, ApplicationDataCenterFileDataModelProvider>();
        services.AddScoped<ApplicationDataCenterSchemaMigrator>();
        services.AddScoped<ApplicationMappingCacheMigrationService>();
        services.AddScoped<ApplicationDataCenterSeedService>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataSourceSqlitePathApprovalEntity>, ApplicationDataSourceSqlitePathApprovalDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataSourceSqlitePathApprovalAuditEntity>, ApplicationDataSourceSqlitePathApprovalAuditDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataMutationLedgerEntity>, ApplicationDataMutationLedgerDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationMappingCacheEntity>, ApplicationMappingCacheDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationMappingCacheColumnEntity>, ApplicationMappingCacheColumnDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationMappingCacheParameterEntity>, ApplicationMappingCacheParameterDataPermissionDescriptor>();
    }
}

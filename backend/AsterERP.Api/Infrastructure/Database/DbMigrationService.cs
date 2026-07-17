using AsterERP.Api.Infrastructure.Abp.CoreShell;
using AsterERP.Api.Infrastructure.Abp.AiCenter;
using AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure.Abp.AsterScene;
using AsterERP.Api.Infrastructure.Abp.Im;
using AsterERP.Api.Infrastructure.Abp.PlatformFoundation;
using AsterERP.Api.Infrastructure.Abp.PlatformPublish;
using AsterERP.Api.Infrastructure.Abp.RuntimeCore;
using AsterERP.Api.Infrastructure.Abp.SystemAdministration;
using AsterERP.Api.Infrastructure.Abp.TenantApps;
using AsterERP.Api.Infrastructure.Abp.WorkflowApproval;
using AsterERP.Api.Application.ApplicationConsole;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Database;

public sealed class DbMigrationService(
    ISqlSugarClient db,
    CoreShellSchemaMigrator coreShellSchemaMigrator,
    AsterErpSystemAdministrationSchemaMigrator systemAdministrationSchemaMigrator,
    PlatformFoundationSchemaMigrator platformFoundationSchemaMigrator,
    PlatformPublishSchemaMigrator platformPublishSchemaMigrator,
    TenantAppsSchemaMigrator tenantAppsSchemaMigrator,
    WorkflowApprovalSchemaMigrator workflowApprovalSchemaMigrator,
    RuntimeCoreSchemaMigrator runtimeCoreSchemaMigrator,
    ImSchemaMigrator imSchemaMigrator,
    AiCenterSchemaMigrator aiCenterSchemaMigrator,
    AsterSceneSchemaMigrator asterSceneSchemaMigrator,
    ApplicationDataCenterSchemaMigrator applicationDataCenterSchemaMigrator,
    ApplicationDevelopmentCenterSchemaMigrator applicationDevelopmentCenterSchemaMigrator,
    ApplicationDatabaseBindingMigrationService applicationDatabaseBindingMigrationService,
    IServiceProvider serviceProvider,
    ILogger<DbMigrationService> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await coreShellSchemaMigrator.MigrateAsync(db, cancellationToken);
        await systemAdministrationSchemaMigrator.MigrateAsync(serviceProvider, db, cancellationToken);
        await platformFoundationSchemaMigrator.MigrateAsync(db, cancellationToken);
        await platformPublishSchemaMigrator.MigrateAsync(db, cancellationToken);
        await tenantAppsSchemaMigrator.MigrateAsync(db, cancellationToken);
        await workflowApprovalSchemaMigrator.MigrateAsync(serviceProvider, db, cancellationToken);
        await runtimeCoreSchemaMigrator.MigrateAsync(db, cancellationToken);
        await imSchemaMigrator.MigrateAsync(db, cancellationToken);
        await aiCenterSchemaMigrator.MigrateAsync(db, cancellationToken);
        await asterSceneSchemaMigrator.MigrateAsync(db, cancellationToken);
        await applicationDataCenterSchemaMigrator.MigrateAsync(db, cancellationToken);
        await applicationDatabaseBindingMigrationService.MigrateAsync(cancellationToken);
        await applicationDevelopmentCenterSchemaMigrator.EnsureCurrentSchemaAsync(db, cancellationToken);

        logger.LogInformation(
            "Database migration completed through the explicit ABP schema migrator chain");
    }
}

using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;
using AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Abp.PlatformFoundation;
using AsterERP.Api.Infrastructure.Abp.RuntimeCore;
using AsterERP.Api.Infrastructure.Abp.SystemAdministration;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;

[DependsOn(
    typeof(AsterErpApplicationDataCenterModule),
    typeof(AsterErpPlatformFoundationModule),
    typeof(AsterErpRuntimeCoreModule),
    typeof(AsterErpSystemAdministrationModule))]
public sealed class AsterErpApplicationDevelopmentCenterModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        services.AddScoped<ApplicationDevelopmentSchemaCompiler>();
        services.AddScoped<ApplicationDevelopmentSchemaValidator>();
        services.AddScoped<ApplicationPageMicroflowBindingValidator>();
        services.AddScoped<ApplicationPageRuntimeEnvironmentCheckService>();
        services.AddScoped<ApplicationDevelopmentPageRevisionGuard>();
        services.AddScoped<ApplicationDevelopmentCenterService>();
        services.AddScoped<ApplicationDevelopmentCenterSchemaMigrator>();
        services.AddScoped<ApplicationDevelopmentCenterSeedService>();
        services.AddScoped<ApplicationDesignerDocumentMigrationService>();
        services.AddScoped<ApplicationLegacyPageSchemaMigrationService>();
        services.AddScoped<ApplicationDesignerDocumentStore>();
        services.AddScoped<ApplicationDesignerArtifactPublisher>();
        services.AddScoped<ApplicationDesignerArtifactRollbackService>();
        services.AddScoped<ApplicationDesignerMigrationRunService>();
        services.AddScoped<ApplicationMonitoringEventService>();
    }

    public static void RegisterDataFilters(IDataPermissionFilterRegistry registry)
    {
        registry.RegisterWorkspaceFilter(typeof(ApplicationDevelopmentVersionEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationDevelopmentModuleEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationDevelopmentPageEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationSharedResourceEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationDesignerDocumentEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationDesignerRevisionEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationDesignerMigrationEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationDesignerRuntimeArtifactEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationDesignerEditorSessionEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationDesignerPublishRecordEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationDesignerMigrationRunEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationDesignerMigrationWatermarkEntity));
        registry.RegisterWorkspaceFilter(typeof(ApplicationMonitoringEventEntity));
    }
}

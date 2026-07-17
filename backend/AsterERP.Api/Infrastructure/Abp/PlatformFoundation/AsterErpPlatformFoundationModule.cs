using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.Platform;
using AsterERP.Api.Application.Platform.Applications;
using AsterERP.Api.Application.Platform.TenantApps;
using AsterERP.Api.Application.Platform.Tenants;
using AsterERP.Api.Application.Platform.UserAppRoles;
using AsterERP.Api.Application.Platform.UserTenants;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Abp.CoreShell;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.PlatformFoundation;

[DependsOn(typeof(AsterErpCoreShellModule))]
public sealed class AsterErpPlatformFoundationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        services.AddScoped<PlatformFoundationSchemaMigrator>();
        services.AddScoped<PlatformAccessGuard>();
        services.AddScoped<IPlatformTenantService, PlatformTenantService>();
        services.AddScoped<IPlatformApplicationService, PlatformApplicationService>();
        services.AddScoped<IPlatformApplicationWorkspaceProvisioningService, PlatformApplicationWorkspaceProvisioningService>();
        services.AddScoped<IPlatformApplicationEntryService, PlatformApplicationEntryService>();
        services.AddScoped<IPlatformTenantAppService, PlatformTenantAppService>();
        services.AddScoped<IPlatformUserTenantService, PlatformUserTenantService>();
        services.AddScoped<IPlatformUserAppRoleService, PlatformUserAppRoleService>();
        services.AddScoped<IApplicationConsoleService, ApplicationConsoleService>();
        services.AddScoped<ApplicationManagedSqliteDatabaseResolver>();
        services.AddScoped<ApplicationDatabaseBindingResolver>();
        services.AddScoped<ApplicationDatabaseBindingMigrationService>();
        services.AddScoped<IApplicationDatabaseConnectionFactory, ApplicationDatabaseConnectionFactory>();
        services.AddScoped<ApplicationSystemAdministrationSchemaInitializer>();
        services.AddScoped<ApplicationRbacBaselineSeeder>();
        services.AddScoped<ApplicationWorkflowAcceptanceBaselineSeeder>();
        services.AddScoped<ApplicationShellCapabilityResolver>();
        services.AddScoped<ApplicationDatabaseBaselineSeeder>();
        services.AddScoped<ApplicationDataCenterApplicationDatabaseSchemaInitializer>();
        services.AddScoped<ApplicationWorkflowSchemaInitializer>();
        services.AddScoped<ApplicationDatabaseSchemaInitializer>();
        services.AddScoped<ApplicationDatabaseCapabilityReader>();
        services.AddScoped<ApplicationDatabasePermissionReader>();
        services.AddDataProtection();
        services.AddSingleton<IApplicationConnectionStringProtector, ApplicationConnectionStringProtector>();
    }
}

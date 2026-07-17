using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Abp.SystemAdministration;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Api.Modules.System.Menus;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.RuntimeCore;

[DependsOn(typeof(AsterErpSystemAdministrationModule))]
public sealed class AsterErpRuntimeCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        services.AddScoped<RuntimeCoreSchemaMigrator>();
        services.AddScoped<IRuntimePageSchemaService, RuntimePageSchemaService>();
        services.AddScoped<IRuntimeDataModelService, RuntimeDataModelService>();
        services.AddScoped<IRuntimeGridViewService, RuntimeGridViewService>();
        services.AddSingleton<RuntimeExpressionHelperCatalog>();
        services.AddSingleton<RuntimeSnowflakeIdGenerator>();
        services.AddScoped<RuntimeValueExpressionEvaluator>();
        services.AddScoped<RuntimeDataImportService>();
        services.AddScoped<RuntimeDataReadPermissionService>();
        services.AddScoped<RuntimeDataMutationPermissionService>();
        services.AddScoped<IDataModelProviderRegistry, DataModelProviderRegistry>();
        services.AddScoped<IDataModelProvider, SystemMenuDataModelProvider>();
    }

    public static void RegisterDataFilters(IDataPermissionFilterRegistry registry)
    {
        registry.RegisterWorkspaceFilter(typeof(SystemDataModelEntity));
        registry.RegisterWorkspaceFilter(typeof(SystemTenantGridViewEntity));
        registry.RegisterWorkspaceFilter(typeof(SystemUserGridViewEntity));
    }
}

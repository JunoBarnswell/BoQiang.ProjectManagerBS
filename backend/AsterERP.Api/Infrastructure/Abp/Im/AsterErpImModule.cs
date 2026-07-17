using AsterERP.Api.Application.Im;
using AsterERP.Api.Infrastructure.Abp.CoreShell;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.Im;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.Im;

[DependsOn(typeof(AsterErpCoreShellModule))]
public sealed class AsterErpImModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        services.AddScoped<IImAccountBindingService, ImAccountBindingService>();
        services.AddScoped<IImUserDirectoryService, ImUserDirectoryService>();
        services.AddScoped<IImConversationService, ImConversationService>();
        services.AddScoped<IImRealtimePushService, ImRealtimePushService>();
        services.AddSingleton<IImPresenceService, ImPresenceService>();
        services.AddScoped<ImSchemaMigrator>();
        services.AddScoped<ImSeedService>();
    }

    public static void RegisterDataFilters(IDataPermissionFilterRegistry registry)
    {
        registry.RegisterImTenantFilter(typeof(ImAccountBindingEntity));
        registry.RegisterImTenantFilter(typeof(ImConversationEntity));
        registry.RegisterImTenantFilter(typeof(ImConversationParticipantEntity));
        registry.RegisterImTenantFilter(typeof(ImMessageEntity));
        registry.RegisterImTenantFilter(typeof(ImMessageDeliveryLogEntity));
    }
}

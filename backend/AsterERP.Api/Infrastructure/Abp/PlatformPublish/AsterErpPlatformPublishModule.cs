using AsterERP.Api.Application.Platform.ApplicationPublishing;
using AsterERP.Api.Infrastructure.Abp.PlatformFoundation;
using AsterERP.Api.Infrastructure.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.PlatformPublish;

[DependsOn(typeof(AsterErpPlatformFoundationModule))]
public sealed class AsterErpPlatformPublishModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        services.AddScoped<IPlatformApplicationPublishService, PlatformApplicationPublishService>();
        services.AddScoped<PlatformApplicationPublishRunner>();
        services.AddTransient<PlatformApplicationPublishJob>();
        services.AddSingleton<ApplicationPublishLockRegistry>();
        services.Configure<ApplicationPublishOptions>(context.Configuration.GetSection("ApplicationPublish"));
        services.AddSingleton<ApplicationPublishPathGuard>();
        services.AddSingleton<ApplicationPublishModuleFileMapLoader>();
        services.AddSingleton<ApplicationPublishModuleClosureResolver>();
        services.AddSingleton<ApplicationPublishSourceCollector>();
        services.AddSingleton<ApplicationPublishFrontendTargetWriter>();
        services.AddSingleton<ApplicationPublishPackageWriter>();
        services.AddSingleton<ApplicationPublishLeakScanner>();
        services.AddSingleton<IApplicationPublishProcessRunner, ApplicationPublishProcessRunner>();
        services.AddScoped<PlatformPublishSchemaMigrator>();
    }
}

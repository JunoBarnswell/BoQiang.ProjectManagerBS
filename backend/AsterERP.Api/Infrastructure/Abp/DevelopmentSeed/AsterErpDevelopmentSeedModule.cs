using AsterERP.Api.Infrastructure.Abp.TenantApps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.DevelopmentSeed;

[DependsOn(typeof(AsterErpTenantAppsModule))]
public sealed class AsterErpDevelopmentSeedModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        if (!context.Services.GetSingletonInstance<IHostEnvironment>().IsDevelopment() &&
            !context.Services.GetSingletonInstance<IHostEnvironment>().IsEnvironment("Testing"))
        {
            return;
        }

        context.Services.AddScoped<DevelopmentSeedDataService>();
        context.Services.Configure<DevelopmentSeedOptions>(context.Configuration.GetSection("DevelopmentSeed"));
        context.Services.AddSingleton<IValidateOptions<DevelopmentSeedOptions>, DevelopmentSeedOptionsValidator>();
        context.Services.AddScoped<IDevelopmentSeedService>(serviceProvider =>
            serviceProvider.GetRequiredService<DevelopmentSeedDataService>());
    }
}

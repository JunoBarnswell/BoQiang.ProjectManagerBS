using AsterERP.Api.Application.Tenant;
using AsterERP.Api.Application.Tenant.Apps;
using AsterERP.Api.Infrastructure.Abp.PlatformFoundation;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.TenantApps;

[DependsOn(typeof(AsterErpPlatformFoundationModule))]
public sealed class AsterErpTenantAppsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<TenantAppsSchemaMigrator>();
        context.Services.AddScoped<TenantAccessGuard>();
        context.Services.AddScoped<ITenantAppService, TenantAppService>();
    }
}

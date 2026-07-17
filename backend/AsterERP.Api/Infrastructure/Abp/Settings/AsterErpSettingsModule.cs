using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Modularity;
using Volo.Abp.Settings;

namespace AsterERP.Api.Infrastructure.Abp.Settings;

[DependsOn(typeof(AbpSettingsModule))]
public sealed class AsterErpSettingsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(ServiceDescriptor.Transient<ISettingStore, SystemParameterSettingStore>());
    }
}

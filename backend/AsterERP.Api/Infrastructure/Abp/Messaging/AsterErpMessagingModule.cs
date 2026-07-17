using AsterERP.Api.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus;
using Volo.Abp.MailKit;
using Volo.Abp.Modularity;
using Volo.Abp.Sms;

namespace AsterERP.Api.Infrastructure.Abp.Messaging;

[DependsOn(
    typeof(AbpEmailingModule),
    typeof(AbpEventBusModule),
    typeof(AbpMailKitModule),
    typeof(AbpSmsModule))]
public sealed class AsterErpMessagingModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClient();
        context.Services.TryAddScoped<IAsterErpMessagingService, AsterErpMessagingService>();
        context.Services.AddTransient<MessageSendCompletedEventHandler>();
        context.Services.Replace(ServiceDescriptor.Transient<ISmsSender, AsterErpSmsSender>());
    }
}

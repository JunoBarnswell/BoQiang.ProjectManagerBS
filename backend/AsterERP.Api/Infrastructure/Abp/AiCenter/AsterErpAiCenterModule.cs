using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Api.Infrastructure.Scheduling;
using AsterERP.Api.Infrastructure.Abp.PlatformFoundation;
using AsterERP.Api.Infrastructure.Abp.WorkflowApproval;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

[DependsOn(
    typeof(AsterErpPlatformFoundationModule),
    typeof(AsterErpWorkflowApprovalModule))]
public sealed class AsterErpAiCenterModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        AiCenterServiceRegistrar.RegisterApplicationServices(services);
        AiCenterServiceRegistrar.RegisterInfrastructureServices(services);
        services.AddSingleton<IFlowiseScheduleScheduler, HangfireFlowiseScheduleScheduler>();
        services.AddHostedService<FlowiseScheduleStartupSynchronizer>();
        services.AddScoped<AiCenterSchemaMigrator>();
        services.AddScoped<AiCenterModuleSeeder>();
    }

    public static void RegisterDataFilters(IDataPermissionFilterRegistry registry)
    {
        AiCenterDataFilterRegistrar.Register(registry);
    }
}

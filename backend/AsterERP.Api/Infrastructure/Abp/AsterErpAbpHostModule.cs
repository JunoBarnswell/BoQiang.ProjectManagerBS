using AsterERP.Api.Infrastructure.Workflows;
using AsterERP.Api.Infrastructure.Abp.CoreShell;
using AsterERP.Api.Infrastructure.Abp.DevelopmentSeed;
using AsterERP.Api.Infrastructure.Abp.PlatformFoundation;
using AsterERP.Api.Infrastructure.Abp.PlatformPublish;
using AsterERP.Api.Infrastructure.Abp.RuntimeCore;
using AsterERP.Api.Infrastructure.Abp.TenantApps;
using AsterERP.Api.Infrastructure.Abp.Audit;
using AsterERP.Api.Infrastructure.Abp.AsterScene;
using AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure.Abp.FileManagement;
using AsterERP.Api.Infrastructure.Abp.Jobs;
using AsterERP.Api.Infrastructure.Abp.Messaging;
using AsterERP.Api.Infrastructure.Abp.ObjectStorage;
using AsterERP.Api.Infrastructure.Abp.Settings;
using AsterERP.Api.Infrastructure.Abp.SystemAdministration;
using AsterERP.Api.Infrastructure.Abp.WorkflowApproval;
using AsterERP.Api.Infrastructure.Abp.AiCenter;
using AsterERP.Api.Infrastructure.Abp.Im;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Workflow.Approval.Core;
using AsterERP.Workflow.Forms.Core;
using AsterERP.Workflow.Core;
using AsterERP.Workflow.DependencyInjection;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Caching;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpCachingModule),
    typeof(AsterErpCoreShellModule),
    typeof(AsterErpDevelopmentSeedModule),
    typeof(AsterErpPlatformFoundationModule),
    typeof(AsterErpPlatformPublishModule),
    typeof(AsterErpAsterSceneModule),
    typeof(AsterErpApplicationDataCenterModule),
    typeof(AsterErpApplicationDevelopmentCenterModule),
    typeof(AsterErpRuntimeCoreModule),
    typeof(AsterErpTenantAppsModule),
    typeof(AsterErpWorkflowApprovalModule),
    typeof(AsterErpAiCenterModule),
    typeof(AsterErpImModule),
    typeof(AsterErpProjectManagementModule),
    typeof(AsterErpWorkflowApprovalCoreModule),
    typeof(AsterErpWorkflowFormsCoreModule),
    typeof(AsterErpWorkflowCoreModule),
    typeof(AsterErpWorkflowDependencyInjectionModule),
    typeof(AsterErpSettingsModule),
    typeof(AsterErpSystemAdministrationModule),
    typeof(AsterErpMessagingModule),
    typeof(AsterErpObjectStorageModule),
    typeof(AsterErpFileManagementModule),
    typeof(AsterErpJobsModule),
    typeof(AsterErpAuditModule))]
public sealed class AsterErpAbpHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<WorkflowNotificationWorker>();
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.ServiceProvider.GetRequiredService<DbInitializer>().InitializeAsync();

        context.ServiceProvider
            .GetRequiredService<IRecurringJobManager>()
            .AddOrUpdate<WorkflowNotificationWorker>(
                "workflow-notification-worker",
                "scheduled-jobs",
                worker => worker.ProcessAsync(),
                "*/20 * * * * *",
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Local
                });
    }
}

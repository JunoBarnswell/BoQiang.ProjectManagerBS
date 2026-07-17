using AsterERP.Api.Application.System.Announcements;
using AsterERP.Api.Application.System.Dicts;
using AsterERP.Api.Application.System.Excel;
using AsterERP.Api.Application.System.Files;
using AsterERP.Api.Application.System.Foundation;
using AsterERP.Api.Application.System.InfrastructureSettings;
using AsterERP.Api.Application.System.LoginLogs;
using AsterERP.Api.Application.System.Menus;
using AsterERP.Api.Application.System.Notifications;
using AsterERP.Api.Application.System.OnlineUsers;
using AsterERP.Api.Application.System.Organizations;
using AsterERP.Api.Application.System.Parameters;
using AsterERP.Api.Application.System.Printing;
using AsterERP.Api.Application.System.QueryViews;
using AsterERP.Api.Application.System.Roles;
using AsterERP.Api.Application.System.ScheduledJobs;
using AsterERP.Api.Application.System.Users;
using AsterERP.Api.Domain.System.ScheduledJobs;
using AsterERP.Api.Infrastructure.Abp.FileManagement;
using AsterERP.Api.Infrastructure.Abp.Jobs;
using AsterERP.Api.Infrastructure.Abp.Messaging;
using AsterERP.Api.Infrastructure.Abp.ObjectStorage;
using AsterERP.Api.Infrastructure.Abp.PlatformFoundation;
using AsterERP.Api.Infrastructure.Abp.Settings;
using AsterERP.Api.Infrastructure.CodeRules;
using AsterERP.Api.Infrastructure.Dicts;
using AsterERP.Api.Infrastructure.Files;
using AsterERP.Api.Infrastructure.Logging;
using AsterERP.Api.Infrastructure.Messaging;
using AsterERP.Api.Infrastructure.QueryViews;
using AsterERP.Api.Infrastructure.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.SystemAdministration;

[DependsOn(
    typeof(AsterErpPlatformFoundationModule),
    typeof(AsterErpSettingsModule),
    typeof(AsterErpMessagingModule),
    typeof(AsterErpObjectStorageModule),
    typeof(AsterErpFileManagementModule),
    typeof(AsterErpJobsModule))]
public sealed class AsterErpSystemAdministrationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddScoped<AsterErpSystemAdministrationSchemaMigrator>();

        services.AddScoped<IDictManagementService, DictManagementService>();
        services.AddScoped<ISystemMenuService, SystemMenuService>();
        services.AddScoped<ISystemDepartmentService, SystemDepartmentService>();
        services.AddScoped<ISystemPositionService, SystemPositionService>();
        services.AddScoped<ISystemRoleService, SystemRoleService>();
        services.AddScoped<ISystemUserService, SystemUserService>();
        services.AddScoped<ISystemFoundationService, SystemFoundationService>();
        services.AddScoped<IQueryViewResourceService, QueryViewResourceService>();
        services.AddScoped<IQueryViewDesignerService, QueryViewDesignerService>();
        services.AddScoped<IQueryViewRuntimeService, QueryViewRuntimeService>();
        services.AddScoped<IQueryViewExportService, QueryViewExportService>();
        services.AddScoped<IParameterService, ParameterService>();
        services.AddScoped<IInfrastructureSettingsService, InfrastructureSettingsService>();
        services.AddScoped<IFileAppService, FileAppService>();
        services.AddScoped<PrintWorkspaceResolver>();
        services.AddScoped<PrintTargetCatalog>();
        services.AddScoped<PrintDataProviderRegistry>();
        services.AddScoped<QueryViewListPrintDataProvider>();
        services.AddScoped<IPrintDataProvider, SystemUserDetailPrintDataProvider>();
        services.AddScoped<IPrintDataProvider, SystemRoleDetailPrintDataProvider>();
        services.AddScoped<IPrintDataProvider, SystemFileDetailPrintDataProvider>();
        services.AddScoped<SystemPrintTemplateService>();
        services.AddScoped<SystemPrintCustomElementService>();
        services.AddScoped<SystemPrintRuntimeService>();
        services.AddScoped<IParameterExcelService, ParameterExcelService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<ILoginLogService, LoginLogService>();
        services.AddScoped<IOnlineUserService, OnlineUserService>();
        services.AddScoped<ScheduledJobTypeCatalog>();
        services.AddScoped<ScheduleExpressionBuilder>();
        services.AddScoped<HttpCallbackDomainPolicy>();
        services.AddScoped<ScheduledJobDomainPolicy>();
        services.AddScoped<IScheduledJobService, ScheduledJobService>();

        services.AddScoped<QueryViewMigrationService>();
        services.AddScoped<IDictService, DictService>();
        services.AddScoped<ICodeRuleService, CodeRuleService>();
        services.AddScoped<IFileContentHashService, Sha256FileContentHashService>();
        services.AddScoped<IFileStorageService, AbpBlobFileStorageService>();
        services.AddScoped<IMessageSendLogWriter, MessageSendLogWriter>();
        services.AddScoped<IOperationLogService, OperationLogService>();
        services.AddScoped<SerialNoGenerator>();
        services.Configure<SchedulerOptions>(context.Configuration.GetSection("Scheduler"));
        services.AddScoped<IScheduledJobScheduler, HangfireScheduledJobScheduler>();
        services.AddScoped<IScheduledJobHttpCallbackClient, ScheduledJobHttpCallbackClient>();
        services.AddScoped<PresetScheduledJobRunner>();
        services.AddScoped<ScheduledJobExecutor>();
        services.AddTransient<ScheduledJobExecutionJob>();
    }
}

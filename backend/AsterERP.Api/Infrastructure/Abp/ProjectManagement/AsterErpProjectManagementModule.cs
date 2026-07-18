using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.CoreShell;
using AsterERP.Api.Infrastructure.Scheduling;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;

namespace AsterERP.Api.Infrastructure.Abp.ProjectManagement;

/// <summary>
/// ProjectManagement 的 ABP 模块边界。
/// 业务应用服务、实体、迁移、权限和 ORM 数据过滤均在该边界内接入。
/// </summary>
[DependsOn(typeof(AsterErpCoreShellModule))]
public sealed class AsterErpProjectManagementModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<ProjectManagementSchemaMigrator>();
        context.Services.AddScoped<ProjectManagementAccessPolicy>();
        context.Services.AddScoped<IProjectManagementActivityService, ProjectManagementActivityService>();
        context.Services.AddScoped<IProjectManagementActivityWriter>(provider => provider.GetRequiredService<IProjectManagementActivityService>());
        context.Services.AddScoped<IProjectManagementAuditService, ProjectManagementAuditService>();
        context.Services.AddScoped<IProjectManagementMaintenanceLock, ProjectManagementMaintenanceLock>();
        context.Services.AddScoped<IProjectManagementRiskConfirmationService, ProjectManagementRiskConfirmationService>();
        context.Services.AddScoped<IProjectManagementBackupService, ProjectManagementBackupService>();
        context.Services.AddScoped<IProjectManagementOperationProgressPublisher, ProjectManagementOperationProgressPublisher>();
        context.Services.AddScoped<IProjectManagementOperationWriter, ProjectManagementOperationWriter>();
        context.Services.AddScoped<IProjectManagementOperationService, ProjectManagementOperationService>();
        context.Services.AddScoped<ProjectManagementWorkspaceValidationExecutor>();
        context.Services.AddTransient<ProjectManagementOperationRunner>();
        context.Services.AddTransient<ProjectManagementOperationJob>();
        context.Services.AddScoped<IProjectManagementSyncJournalWriter, ProjectManagementSyncJournalWriter>();
        context.Services.AddScoped<IProjectManagementTaskCommentService, ProjectManagementTaskCommentService>();
        context.Services.AddScoped<IProjectManagementTaskAttachmentService, ProjectManagementTaskAttachmentService>();
        context.Services.AddScoped<IProjectManagementSyncService, ProjectManagementSyncService>();
        context.Services.AddScoped<IProjectManagementDataSpaceService, ProjectManagementDataSpaceService>();
        context.Services.AddScoped<IProjectManagementNotificationService, ProjectManagementNotificationService>();
        context.Services.AddScoped<IProjectManagementNotificationPublisher>(provider => provider.GetRequiredService<IProjectManagementNotificationService>());
        context.Services.AddScoped<IProjectManagementReminderScheduler, HangfireProjectManagementReminderScheduler>();
        context.Services.AddScoped<IProjectManagementTaskReminderService, ProjectManagementTaskReminderService>();
        context.Services.AddScoped<ProjectManagementReminderExecutionService>();
        context.Services.AddTransient<ProjectManagementReminderExecutionRunner>();
        context.Services.AddTransient<ProjectManagementReminderExecutionJob>();
        context.Services.AddScoped<IProjectManagementSavedViewService, ProjectManagementSavedViewService>();
        context.Services.AddScoped<IProjectManagementSearchService, ProjectManagementSearchService>();
        context.Services.AddScoped<IProjectManagementTaskBatchService, ProjectManagementTaskBatchService>();
        context.Services.AddScoped<ProjectManagementTaskLabelMutation>();
        context.Services.AddScoped<ProjectManagementTaskHierarchy>();
        context.Services.AddScoped<IProjectManagementTaskProgressProjector, ProjectManagementTaskProgressProjector>();
        context.Services.AddSingleton<IProjectManagementRealtimeSubscriptionRegistry, ProjectManagementRealtimeSubscriptionRegistry>();
        context.Services.AddScoped<IProjectManagementRealtimePublisher, ProjectManagementRealtimePublisher>();
        context.Services.AddScoped<IProjectManagementMemberCandidateService, ProjectManagementMemberCandidateService>();
        context.Services.AddScoped<IProjectManagementProjectService, ProjectManagementProjectService>();
        context.Services.AddScoped<IProjectManagementMemberService, ProjectManagementMemberService>();
        context.Services.AddScoped<IProjectManagementMilestoneService, ProjectManagementMilestoneService>();
        context.Services.AddScoped<IProjectManagementTaskService, ProjectManagementTaskService>();
        context.Services.AddScoped<IProjectManagementTaskDependencyService, ProjectManagementTaskDependencyService>();
        context.Services.AddScoped<IProjectManagementLabelService, ProjectManagementLabelService>();
        context.Services.AddScoped<IProjectManagementTaskParticipantService, ProjectManagementTaskParticipantService>();
        context.Services.AddScoped<IProjectManagementTaskTimeLogService, ProjectManagementTaskTimeLogService>();
        context.Services.AddScoped<IProjectManagementTaskTemplateService, ProjectManagementTaskTemplateService>();
        context.Services.AddScoped<IProjectManagementRecycleService, ProjectManagementRecycleService>();
        context.Services.AddScoped<IProjectManagementImConversationService, ProjectManagementImConversationService>();
    }

    public static void RegisterDataFilters(IDataPermissionFilterRegistry registry)
    {
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementProjectEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementProjectMemberEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementMilestoneEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskDependencyEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskParticipantEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementLabelEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskLabelEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskTimeLogEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskTemplateEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskOccurrenceEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementActivityEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskCommentEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskCommentMentionEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskAttachmentEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementNotificationEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementTaskReminderEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementSavedViewEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementSyncJournalEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementSyncDeviceEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementMaintenanceLockEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementBackupEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementOperationEntity));
        registry.RegisterWorkspaceFilter(typeof(ProjectManagementImConversationLinkEntity));
    }
}

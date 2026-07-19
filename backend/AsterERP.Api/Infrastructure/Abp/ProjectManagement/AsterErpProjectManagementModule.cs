using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.CoreShell;
using AsterERP.Api.Infrastructure.Abp.SystemAdministration;
using AsterERP.Api.Infrastructure.ProjectManagement;
using AsterERP.Api.Infrastructure.Scheduling;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;
using Microsoft.Extensions.Configuration;

namespace AsterERP.Api.Infrastructure.Abp.ProjectManagement;

/// <summary>
/// ProjectManagement 的 ABP 模块边界。
/// 业务应用服务、实体、迁移、权限和 ORM 数据过滤均在该边界内接入。
/// </summary>
[DependsOn(
    typeof(AsterErpCoreShellModule),
    typeof(AsterErpSystemAdministrationModule))]
public sealed class AsterErpProjectManagementModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Configure<ProjectManagementTaskRecurrenceOptions>(context.Configuration.GetSection(ProjectManagementTaskRecurrenceOptions.SectionName));
        context.Services.AddScoped<ProjectManagementSchemaMigrator>();
        context.Services.AddScoped<ProjectManagementAccessPolicy>();
        context.Services.AddScoped<IProjectManagementActivityService, ProjectManagementActivityService>();
        context.Services.AddScoped<IProjectManagementActivityWriter>(provider => provider.GetRequiredService<IProjectManagementActivityService>());
        context.Services.AddScoped<IProjectManagementWebhookService, ProjectManagementWebhookService>();
        context.Services.AddTransient<ProjectManagementWebhookDeliveryRunner>();
        context.Services.AddTransient<ProjectManagementWebhookDeliveryJob>();
        context.Services.AddScoped<IProjectManagementAuditService, ProjectManagementAuditService>();
        context.Services.AddScoped<IProjectManagementMaintenanceLock, ProjectManagementMaintenanceLock>();
        context.Services.AddScoped<ProjectManagementWipCoordinator>();
        context.Services.AddScoped<IProjectManagementRiskConfirmationService, ProjectManagementRiskConfirmationService>();
        context.Services.AddScoped<IProjectManagementBackupService, ProjectManagementBackupService>();
        context.Services.AddScoped<IProjectManagementDataSpaceExportService, ProjectManagementDataSpaceExportService>();
        context.Services.AddScoped<IProjectManagementDataSpaceImportService, ProjectManagementDataSpaceImportService>();
        context.Services.AddScoped<IProjectManagementRealtimeTransport, ProjectManagementRealtimeTransport>();
        context.Services.AddScoped<IProjectManagementOperationProgressPublisher, ProjectManagementOperationProgressPublisher>();
        context.Services.AddScoped<IProjectManagementOperationWriter, ProjectManagementOperationWriter>();
        context.Services.AddScoped<IProjectManagementOperationService, ProjectManagementOperationService>();
        context.Services.AddScoped<ProjectManagementReversibleCommandService>();
        context.Services.AddScoped<IProjectManagementReversibleCommandService>(provider => provider.GetRequiredService<ProjectManagementReversibleCommandService>());
        context.Services.AddScoped<IProjectManagementReversibleCommandWriter>(provider => provider.GetRequiredService<ProjectManagementReversibleCommandService>());
        context.Services.AddScoped<IProjectManagementReversibleCommandHandler, ProjectManagementReversibleCommandHandler>();
        context.Services.AddScoped<ProjectManagementWorkspaceValidationExecutor>();
        context.Services.AddScoped<ProjectManagementReportSnapshotExecutor>();
        context.Services.AddScoped<ProjectManagementAuditExportExecutor>();
        context.Services.AddScoped<ProjectManagementDataSpaceExportExecutor>();
        context.Services.AddScoped<ProjectManagementDataSpaceImportExecutor>();
        context.Services.AddScoped<ProjectManagementPurgeFileDeletionExecutor>();
        context.Services.AddScoped<IProjectManagementPurgeFileDeletionService, ProjectManagementPurgeFileDeletionService>();
        context.Services.AddTransient<ProjectManagementOperationRunner>();
        context.Services.AddTransient<ProjectManagementOperationJob>();
        context.Services.AddScoped<IProjectManagementSyncJournalWriter, ProjectManagementSyncJournalWriter>();
        context.Services.AddScoped<IProjectManagementSyncHistoryService, ProjectManagementSyncHistoryService>();
        context.Services.AddScoped<IProjectManagementFileStore, ProjectManagementFileStore>();
        context.Services.AddScoped<IProjectManagementTaskCommentService, ProjectManagementTaskCommentService>();
        context.Services.AddScoped<IProjectManagementTaskAttachmentService, ProjectManagementTaskAttachmentService>();
        context.Services.AddScoped<IProjectManagementExternalApiIdempotencyService, ProjectManagementExternalApiIdempotencyService>();
        context.Services.AddScoped<IProjectManagementExternalApiService, ProjectManagementExternalApiService>();
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
        context.Services.AddScoped<IProjectManagementTaskBatchExecutionService, ProjectManagementTaskBatchExecutionService>();
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
        context.Services.AddScoped<IProjectManagementTaskTemplateCommandService>(provider => (ProjectManagementTaskService)provider.GetRequiredService<IProjectManagementTaskService>());
        context.Services.AddScoped<IProjectManagementTaskOccurrenceCommandService>(provider => (ProjectManagementTaskService)provider.GetRequiredService<IProjectManagementTaskService>());
        context.Services.AddScoped<IProjectManagementTaskRecurrenceScheduler, HangfireProjectManagementTaskRecurrenceScheduler>();
        context.Services.AddScoped<IProjectManagementTaskRecurrenceService, ProjectManagementTaskRecurrenceService>();
        context.Services.AddTransient<ProjectManagementTaskRecurrenceGenerationRunner>();
        context.Services.AddTransient<ProjectManagementTaskRecurrenceGenerationJob>();
        context.Services.AddScoped<IProjectManagementTaskDependencyService, ProjectManagementTaskDependencyService>();
        context.Services.AddScoped<IProjectManagementTaskTemplateDependencyCommandService>(provider => (ProjectManagementTaskDependencyService)provider.GetRequiredService<IProjectManagementTaskDependencyService>());
        context.Services.AddScoped<IProjectManagementLabelService, ProjectManagementLabelService>();
        context.Services.AddScoped<IProjectManagementTaskParticipantService, ProjectManagementTaskParticipantService>();
        context.Services.AddScoped<IProjectManagementTaskTimeLogService, ProjectManagementTaskTimeLogService>();
        context.Services.AddScoped<IProjectManagementTaskTemplateService, ProjectManagementTaskTemplateService>();
        context.Services.AddScoped<IProjectManagementTaskTemplateInstantiationService, ProjectManagementTaskTemplateInstantiationService>();
        context.Services.AddScoped<IProjectManagementRecycleService, ProjectManagementRecycleService>();
        context.Services.AddScoped<IProjectManagementImConversationService, ProjectManagementImConversationService>();
        context.Services.AddScoped<ProjectManagementAutomationService>();
        context.Services.AddScoped<IProjectManagementAutomationService>(provider => provider.GetRequiredService<ProjectManagementAutomationService>());
        context.Services.AddTransient<ProjectManagementAutomationRunner>();
        context.Services.AddTransient<ProjectManagementAutomationWebhookJob>();
        context.Services.AddScoped<IProjectManagementApprovalService, ProjectManagementApprovalService>();
    }

    public static void RegisterDataFilters(IDataPermissionFilterRegistry registry)
    {
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementProjectEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementProjectMemberEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementMilestoneEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskDependencyEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskParticipantEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementLabelEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskLabelEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskTimeLogEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskTemplateEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskOccurrenceEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskRecurrenceEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskRecurrenceOccurrenceEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementActivityEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskCommentEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskCommentMentionEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskAttachmentEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementExternalApiRequestEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementNotificationEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementTaskReminderEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementSavedViewEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementSyncJournalEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementSyncDeviceEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementSyncHistoryEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementMaintenanceLockEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementBackupEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementDataSpaceExportEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementOperationEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementReversibleCommandEntity));
        registry.RegisterProjectManagementFilter(typeof(ProjectManagementImConversationLinkEntity));
    }
}

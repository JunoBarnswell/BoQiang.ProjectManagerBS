using AsterERP.Api.Application.Workflows;
using AsterERP.Api.Application.Workflows.Callbacks;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Workflows;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Infrastructure.Abp.PlatformFoundation;
using AsterERP.Workflow.Approval.Core.Configuration;
using AsterERP.Workflow.Approval.Core.Services.Privilege;
using AsterERP.Workflow.Forms.Core.Configuration;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.DependencyInjection.Persistence;
using AsterERP.Workflow.Persistence.Database;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqlSugar;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.WorkflowApproval;

[DependsOn(
    typeof(AsterErpPlatformFoundationModule),
    typeof(AsterERP.Workflow.Approval.Core.AsterErpWorkflowApprovalCoreModule),
    typeof(AsterERP.Workflow.Forms.Core.AsterErpWorkflowFormsCoreModule),
    typeof(AsterERP.Workflow.Core.AsterErpWorkflowCoreModule),
    typeof(AsterERP.Workflow.DependencyInjection.AsterErpWorkflowDependencyInjectionModule))]
public sealed class AsterErpWorkflowApprovalModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddAsterERPWorkflowApprovalOrm(context.Configuration);
        services.AddAsterERPWorkflowApprovalCaching();
        services.AddScoped<WorkflowApprovalSchemaMigrator>();
        services.AddScoped<WorkflowApprovalSchemaInitializer>();
        services.AddScoped<WorkflowIdentitySyncService>();
        services.AddScoped<IWorkflowIdentityScopeResolver, WorkflowIdentityScopeResolver>();
        services.AddScoped<IWorkflowIdentityCandidateScope, WorkflowIdentityCandidateScope>();
        services.AddScoped<IWorkflowCurrentUserContext, WorkflowCurrentUserContext>();
        services.AddScoped<WorkflowTaskVisibilityService>();
        services.AddScoped<IWorkflowIdentityDisplayService, WorkflowIdentityDisplayService>();

        services.RemoveAll<IWorkflowPersistenceStore>();
        services.AddScoped<IWorkflowPersistenceStore>(serviceProvider =>
        {
            var workspaceDb = serviceProvider.GetRequiredService<IWorkspaceDatabaseAccessor>().GetCurrentDb();
            return new SqlSugarWorkflowPersistenceStore(
                workspaceDb,
                new DatabaseInitializer(workspaceDb, new SqliteSchemaValidator(workspaceDb)),
                serviceProvider);
        });

        services.AddScoped<WorkflowWorkspaceRuntimeInitializer>();
        services.AddAsterERPWorkflowApprovalServices();
        services.AddAsterERPWorkflowFormServices();

        services.AddScoped<IWorkflowModelAppService, WorkflowModelAppService>();
        services.AddScoped<WorkflowBusinessModelLatestValidator>();
        services.AddScoped<IWorkflowDeploymentAppService, WorkflowDeploymentAppService>();
        services.AddScoped<IWorkflowFormResourceAppService, WorkflowFormResourceAppService>();
        services.AddScoped<IWorkflowBindingAppService, WorkflowBindingAppService>();
        services.AddScoped<WorkflowCallbackConfigParser>();
        services.AddScoped<WorkflowCallbackConfigValidator>();
        services.AddScoped<WorkflowCallbackValueResolver>();
        services.AddScoped<WorkflowCallbackExecutor>();
        services.AddScoped<WorkflowParticipantVariableResolver>();
        services.AddScoped<WorkflowTaskNodePolicyResolver>();
        services.AddScoped<WorkflowInstanceAppService>();
        services.AddScoped<IWorkflowInstanceAppService>(provider => provider.GetRequiredService<WorkflowInstanceAppService>());
        services.AddScoped<IWorkflowTaskAppService, WorkflowTaskAppService>();
        services.AddScoped<IWorkflowHistoryAppService, WorkflowHistoryAppService>();
        services.AddScoped<IWorkflowParticipantAppService, WorkflowParticipantAppService>();
        services.AddScoped<IWorkflowNotificationAppService, WorkflowNotificationAppService>();
        services.AddScoped<IWorkflowCategoryAppService, WorkflowCategoryAppService>();
        services.AddScoped<IWorkflowRequestDraftAppService, WorkflowRequestDraftAppService>();
        services.AddScoped<IWorkflowDelegationAppService, WorkflowDelegationAppService>();
        services.AddScoped<IWorkflowWorkCalendarAppService, WorkflowWorkCalendarAppService>();
        services.AddScoped<IWorkflowReportAppService, WorkflowReportAppService>();

        services.AddScoped<WorkflowApprovalSeedService>();
    }

    public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        WorkflowGlobalListenerConfig.RegisterGlobalListeners(context.ServiceProvider);
        return Task.CompletedTask;
    }

    public static void RegisterDataFilters(IDataPermissionFilterRegistry registry)
    {
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowBindingEntity));
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowBusinessInstanceEntity));
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowCallbackLogEntity));
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowCategoryEntity));
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowRequestDraftEntity));
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowDelegationRuleEntity));
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowWorkCalendarEntity));
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowNotificationChannelEntity));
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowMessageTemplateEntity));
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowNodeNotificationRuleEntity));
        registry.RegisterWorkflowWorkspaceFilter(typeof(WorkflowNotificationTaskEntity));
        registry.RegisterWorkflowOwnedFilter(typeof(WorkflowRequestDraftEntity));
    }
}

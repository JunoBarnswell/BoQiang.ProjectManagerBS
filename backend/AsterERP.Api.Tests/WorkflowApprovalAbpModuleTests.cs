using AsterERP.Api.Infrastructure.Abp.WorkflowApproval;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Infrastructure.Workflows;
using AsterERP.Api.Application.Workflows;
using AsterERP.Api.Modules.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowApprovalAbpModuleTests
{
    [Fact]
    public void Workflow_approval_is_owned_by_abp()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpWorkflowApprovalModule)));
    }

    [Fact]
    public void Workflow_approval_registers_real_services_migrator_and_seed_service()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        new AsterErpWorkflowApprovalModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IWorkflowTaskAppService) &&
            descriptor.ImplementationType == typeof(WorkflowTaskAppService));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IWorkflowModelAppService) &&
            descriptor.ImplementationType == typeof(WorkflowModelAppService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(WorkflowApprovalSchemaMigrator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(WorkflowApprovalSeedService));
    }

    [Fact]
    public void Workflow_data_permission_registration_keeps_workspace_and_owner_boundaries()
    {
        var registry = new DataPermissionFilterRegistry();

        AsterErpWorkflowApprovalModule.RegisterDataFilters(registry);

        Assert.Contains(typeof(WorkflowBindingEntity), registry.WorkflowWorkspaceEntityTypes);
        Assert.Contains(typeof(WorkflowBusinessInstanceEntity), registry.WorkflowWorkspaceEntityTypes);
        Assert.Contains(typeof(WorkflowRequestDraftEntity), registry.WorkflowOwnedEntityTypes);
    }
}

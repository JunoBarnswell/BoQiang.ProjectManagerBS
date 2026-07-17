using AsterERP.Api.Infrastructure.Abp.WorkflowApproval;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.Workflows;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowDelegationDataFilterTests
{
    [Fact]
    public void WorkflowModule_ShouldNotRegisterDelegationRulesAsOwnedFilter()
    {
        var registry = new DataPermissionFilterRegistry();

        AsterErpWorkflowApprovalModule.RegisterDataFilters(registry);

        Assert.Contains(typeof(WorkflowDelegationRuleEntity), registry.WorkflowWorkspaceEntityTypes);
        Assert.DoesNotContain(typeof(WorkflowDelegationRuleEntity), registry.WorkflowOwnedEntityTypes);
    }
}

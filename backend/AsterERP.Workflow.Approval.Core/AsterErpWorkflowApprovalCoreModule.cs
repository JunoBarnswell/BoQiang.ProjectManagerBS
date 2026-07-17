using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core;

[DependsOn(
    typeof(AbpGuidsModule),
    typeof(AbpUnitOfWorkModule),
    typeof(AbpTimingModule))]
public sealed class AsterErpWorkflowApprovalCoreModule : AbpModule
{
}

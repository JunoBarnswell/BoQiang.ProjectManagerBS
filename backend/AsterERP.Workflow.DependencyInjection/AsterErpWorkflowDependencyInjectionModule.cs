using AsterERP.Workflow.Core;
using AsterERP.Workflow.Persistence;
using Volo.Abp.Guids;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Modularity;

namespace AsterERP.Workflow.DependencyInjection;

[DependsOn(
    typeof(AbpBackgroundWorkersModule),
    typeof(AsterErpWorkflowCoreModule),
    typeof(AsterErpWorkflowPersistenceModule),
    typeof(AbpGuidsModule),
    typeof(AbpTimingModule),
    typeof(AbpUnitOfWorkModule))]
public sealed class AsterErpWorkflowDependencyInjectionModule : AbpModule
{
}

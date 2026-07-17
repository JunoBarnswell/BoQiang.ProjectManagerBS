using Volo.Abp.BackgroundJobs.Hangfire;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.Jobs;

[DependsOn(
    typeof(AbpBackgroundJobsHangfireModule),
    typeof(AbpDistributedLockingModule))]
public sealed class AsterErpJobsModule : AbpModule
{
}

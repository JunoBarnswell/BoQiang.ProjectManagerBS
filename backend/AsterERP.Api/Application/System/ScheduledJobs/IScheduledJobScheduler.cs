using AsterERP.Api.Modules.System.ScheduledJobs;

namespace AsterERP.Api.Application.System.ScheduledJobs;

public interface IScheduledJobScheduler
{
    Task<string> EnqueueAsync(SystemScheduledJobEntity job, CancellationToken cancellationToken = default);

    Task RegisterOrUpdateAsync(SystemScheduledJobEntity job, CancellationToken cancellationToken = default);

    Task RemoveAsync(SystemScheduledJobEntity job, CancellationToken cancellationToken = default);
}

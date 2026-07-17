using AsterERP.Contracts.System.ScheduledJobs;

namespace AsterERP.Api.Infrastructure.Scheduling;

public interface IScheduledJobHttpCallbackClient
{
    Task<string> SendAsync(HttpCallbackConfigDto callback, CancellationToken cancellationToken = default);
}

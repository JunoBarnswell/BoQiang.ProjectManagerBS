using Volo.Abp.BackgroundJobs;

namespace AsterERP.Api.Application.Platform.ApplicationPublishing;

public sealed class PlatformApplicationPublishJob(
    PlatformApplicationPublishRunner runner) : IAsyncBackgroundJob<PlatformApplicationPublishJobArgs>
{
    public Task ExecuteAsync(PlatformApplicationPublishJobArgs args) => runner.ExecuteAsync(args.TaskId);
}

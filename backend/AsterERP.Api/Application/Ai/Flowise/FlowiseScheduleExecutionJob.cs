using Volo.Abp.BackgroundJobs;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseScheduleExecutionJob(
    FlowiseScheduleExecutionRunner runner) : IAsyncBackgroundJob<FlowiseScheduleExecutionJobArgs>
{
    public Task ExecuteAsync(FlowiseScheduleExecutionJobArgs args) =>
        runner.ExecuteAsync(args);
}

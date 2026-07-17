using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Job;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowJobAsyncBoundaryTests
{
    [Fact]
    public async Task TimerScheduler_StopsPollingWhenCancellationIsRequested()
    {
        using var cancellation = new CancellationTokenSource();
        using var jobExecutor = new AsyncJobExecutorImplementation(new JobManagerImplementation());
        using var scheduler = new TimerScheduler(new JobManagerImplementation(), jobExecutor);

        scheduler.Start(TimeSpan.FromSeconds(30), cancellation.Token);
        cancellation.Cancel();

        await scheduler.StopAsync();
    }

    [Fact]
    public async Task TimerScheduler_StopAsyncWaitsForPollingTask()
    {
        using var jobExecutor = new AsyncJobExecutorImplementation(new JobManagerImplementation());
        using var scheduler = new TimerScheduler(new JobManagerImplementation(), jobExecutor);

        scheduler.Start(TimeSpan.FromMilliseconds(10));
        await scheduler.StopAsync();

        scheduler.Start(TimeSpan.FromMilliseconds(10));
        await scheduler.StopAsync();
    }
}

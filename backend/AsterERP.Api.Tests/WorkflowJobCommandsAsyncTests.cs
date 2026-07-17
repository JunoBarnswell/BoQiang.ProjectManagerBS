using System.Reflection;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Job;
using ProcessDefinitionInfoEntity = AsterERP.Workflow.Persistence.Entities.ProcessDefinitionInfoEntity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowJobCommandsAsyncTests
{
    [Fact]
    public async Task AcquireCommands_UseAsyncJobLifecycleOperations()
    {
        var manager = new JobManagerImplementation();
        var job = await manager.CreateAsyncJobAsync("execution-1", "process-1", "definition-1", false);
        job.Retries = 1;
        await manager.ScheduleAsyncJobAsync(job);
        var context = CreateContext(manager);

        var executable = await new AcquireJobsCmd(10, "worker-1", 1000).ExecuteAsync(context);
        var timer = await new AcquireTimerJobsCmd(10, "worker-1", 1000).ExecuteAsync(context);

        Assert.True(executable.Contains(job.Id!));
        Assert.Equal(0, timer.Size);
    }

    [Fact]
    public async Task CancelAndDeleteCommands_ForwardCancellationTokenAndCompleteAsync()
    {
        var manager = new JobManagerImplementation();
        var job = await manager.CreateAsyncJobAsync("execution-1", "process-1", "definition-1", false);
        job.Retries = 1;
        await manager.ScheduleAsyncJobAsync(job);
        var context = CreateContext(manager);

        using var cancellation = new CancellationTokenSource();
        await new CancelJobsCmd("execution-1").ExecuteAsync(context, cancellation.Token);
        Assert.Empty(await manager.GetJobsAsync());

        var secondJob = await manager.CreateAsyncJobAsync("execution-2", "process-1", "definition-1", false);
        secondJob.Retries = 1;
        await manager.ScheduleAsyncJobAsync(secondJob);
        await new DeleteJobCmd(secondJob.Id!).ExecuteAsync(context, cancellation.Token);

        Assert.Null(await manager.GetJobAsync(secondJob.Id!));
    }

    [Fact]
    public async Task RetryCommands_UseAsyncLifecycleOperationsAndPreserveJobTypeValidation()
    {
        var manager = new JobManagerImplementation();
        var job = await manager.CreateAsyncJobAsync("execution-1", "process-1", "definition-1", false);
        job.Retries = 1;
        await manager.ScheduleAsyncJobAsync(job);
        var context = CreateContext(manager);

        using var cancellation = new CancellationTokenSource();
        await new SetJobRetriesCmd(job.Id!, 4).ExecuteAsync(context, cancellation.Token);

        Assert.Equal(4, (await manager.GetJobAsync(job.Id!))!.Retries);
        await Assert.ThrowsAsync<WorkflowEngineObjectNotFoundException>(() =>
            new SetTimerJobRetriesCmd(job.Id!, 2).ExecuteAsync(context, cancellation.Token));
    }

    private static ICommandContext CreateContext(IJobManager manager)
    {
        var configuration = new ProcessEngineConfiguration { JobManager = manager };
        var context = DispatchProxy.Create<ICommandContext, CommandContextProxy>();
        ((CommandContextProxy)(object)context).Configuration = configuration;
        return context;
    }

    private class CommandContextProxy : DispatchProxy
    {
        public IProcessEngineConfiguration Configuration { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_ProcessEngineConfiguration")
                return Configuration;

            throw new NotSupportedException($"Unexpected command context member: {targetMethod?.Name}");
        }
    }
}

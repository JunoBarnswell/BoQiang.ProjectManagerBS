using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Job;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowBehaviorAsyncBoundaryTests
{
    [Fact]
    public async Task EventCancelledByEventGatewayAsync_AwaitsTimerCancellationBeforeEndingExecution()
    {
        var jobManager = new JobManagerImplementation();
        var behavior = new IntermediateCatchTimerEventActivityBehavior(
            new TimerEventDefinition { TimeDate = "2030-01-01T00:00:00Z" },
            jobManager: jobManager);
        var execution = CreateExecution();

        await behavior.ExecuteAsync(execution);
        Assert.NotNull(await jobManager.GetTimerJobByExecutionAndActivityAsync(execution.Id, execution.CurrentActivityId!));

        await behavior.EventCancelledByEventGatewayAsync(execution);

        Assert.Null(await jobManager.GetTimerJobByExecutionAndActivityAsync(execution.Id, execution.CurrentActivityId!));
        Assert.True(execution.IsEnded);
        Assert.False(execution.IsActive);
    }

    [Fact]
    public async Task EventCancelledByEventGatewayAsync_PropagatesCancellationFailureAndToken()
    {
        var expectedException = new InvalidOperationException("cancel failed");
        var jobManager = new RecordingJobManager(expectedException);
        var behavior = new IntermediateCatchTimerEventActivityBehavior(
            new TimerEventDefinition { TimeDate = "2030-01-01T00:00:00Z" },
            jobManager: jobManager);
        var execution = CreateExecution();
        using var cancellationSource = new CancellationTokenSource();

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.EventCancelledByEventGatewayAsync(execution, cancellationSource.Token));

        Assert.Same(expectedException, actualException);
        Assert.Equal(cancellationSource.Token, jobManager.ReceivedCancellationToken);
        Assert.False(execution.IsEnded);
        Assert.True(execution.IsActive);
    }

    private static ExecutionEntity CreateExecution()
    {
        return new ExecutionEntity
        {
            Id = "execution-1",
            ProcessInstanceId = "process-instance-1",
            ProcessDefinitionId = "process-definition-1",
            CurrentActivityId = "timer-1",
            IsActive = true,
            IsEnded = false
        };
    }
}

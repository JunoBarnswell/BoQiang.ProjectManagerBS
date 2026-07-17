using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Agenda;
using AsterERP.Workflow.Core.Engine;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowAgendaAsyncTests
{
    [Fact]
    public async Task ExecuteNextAsync_PropagatesCancellationBeforeRunningOperation()
    {
        var agenda = new WorkflowEngineAgenda((IProcessEngineConfiguration)null!);
        var executed = false;
        agenda.PlanOperation(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => agenda.ExecuteNextAsync(cancellation.Token));

        Assert.False(executed);
        Assert.False(agenda.IsEmpty);
    }

    [Fact]
    public async Task ExecuteNextAsync_AwaitsPlannedOperation()
    {
        var agenda = new WorkflowEngineAgenda((IProcessEngineConfiguration)null!);
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        agenda.PlanOperation(() => completed.Task);

        var execution = agenda.ExecuteNextAsync();
        Assert.False(execution.IsCompleted);

        completed.SetResult(true);
        await execution;

        Assert.True(agenda.IsEmpty);
    }
}

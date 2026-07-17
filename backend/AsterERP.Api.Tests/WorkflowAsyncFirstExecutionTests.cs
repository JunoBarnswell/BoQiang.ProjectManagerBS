using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Listener;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowAsyncFirstExecutionTests
{
    [Fact]
    public async Task DelegateInvocation_UsesAsyncPathAndRejectsBlockingPath()
    {
        var execution = new ExecutionEntity { Id = "execution-1" };
        using var cancellation = new CancellationTokenSource();
        var activity = new CapturingActivityBehavior();
        var invocation = new ActivityBehaviorInvocation(activity, execution, cancellation.Token);

        Assert.Throws<NotSupportedException>(invocation.Proceed);

        await invocation.ProceedAsync();

        Assert.Same(execution, invocation.Result);
        Assert.Equal(cancellation.Token, activity.CancellationToken);
    }

    [Fact]
    public async Task TransactionListener_UsesAsyncPathAndPreservesCancellation()
    {
        var execution = new ExecutionEntity { Id = "execution-1" };
        var listener = new CapturingExecutionListener();
        var transactionListener = new ExecuteExecutionListenerTransactionListener(listener, execution);
        using var cancellation = new CancellationTokenSource();
        var workflowEvent = new WorkflowEntityEvent(WorkflowEventType.PROCESS_STARTED);

        Assert.Throws<NotSupportedException>(() => transactionListener.OnEvent(workflowEvent));

        await transactionListener.OnEventAsync(workflowEvent, cancellation.Token);

        Assert.Equal(cancellation.Token, listener.CancellationToken);
        Assert.Equal("execution-1", listener.ExecutionId);
    }

    [Fact]
    public async Task RetryInterceptor_UsesCancellableAsyncDelayAndRetriesOptimisticLocking()
    {
        var interceptor = new RetryInterceptor
        {
            NumOfRetries = 2,
            WaitTimeInMs = 1,
            WaitIncreaseFactor = 2
        };
        var attempts = 0;

        var result = await interceptor.ExecuteAsync(
            new TestCommand(),
            _ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new WorkflowEngineOptimisticLockingException("write conflict");
                }

                return Task.FromResult(42);
            });

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
        Assert.Throws<NotSupportedException>(() => interceptor.Execute(new TestCommand(), _ => 42));
    }

    [Fact]
    public async Task RetryInterceptor_StopsDuringBackoffWhenCancelled()
    {
        var interceptor = new RetryInterceptor
        {
            NumOfRetries = 3,
            WaitTimeInMs = 5000
        };
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;

        var execution = interceptor.ExecuteAsync(
            new TestCommand(),
            _ =>
            {
                attempts++;
                throw new WorkflowEngineOptimisticLockingException("write conflict");
            },
            cancellation.Token);

        await Task.Delay(25);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.Equal(1, attempts);
    }

    private sealed class CapturingActivityBehavior : IBpmnActivityBehavior
    {
        public CancellationToken CancellationToken { get; private set; }

        public Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingExecutionListener : AsterERP.Workflow.Core.Listener.IExecutionListener
    {
        public string? Event => null;
        public string? ExecutionId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
        {
            ExecutionId = execution.Id;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TestCommand : ICommand<int>
    {
        public int Execute(ICommandContext context) => 42;

        public Task<int> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(42);
    }
}

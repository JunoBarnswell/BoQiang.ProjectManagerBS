using AsterERP.Workflow.Core.Event;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowEventDispatcherAsyncTests
{
    [Fact]
    public async Task DispatchEventAsync_AwaitsListenerAndPropagatesCancellation()
    {
        var dispatcher = new EventDispatcherImplementation();
        var listener = new CapturingListener();
        dispatcher.AddEventListener(listener, WorkflowEventType.PROCESS_STARTED);
        using var cancellationTokenSource = new CancellationTokenSource();

        var workflowEvent = WorkflowEventBuilder.CreateProcessStartedEvent("instance-1", "definition-1", "order-1");
        await dispatcher.DispatchEventAsync(workflowEvent, cancellationTokenSource.Token);

        Assert.Equal(workflowEvent, listener.ReceivedEvent);
        Assert.Equal(cancellationTokenSource.Token, listener.ReceivedCancellationToken);
    }

    private sealed class CapturingListener : IWorkflowEventListener
    {
        public IWorkflowEvent? ReceivedEvent { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }
        public bool IsFailOnException => true;

        public void OnEvent(IWorkflowEvent @event)
        {
            throw new InvalidOperationException("The async contract must be used by async dispatch.");
        }

        public Task OnEventAsync(IWorkflowEvent @event, CancellationToken cancellationToken = default)
        {
            ReceivedEvent = @event;
            ReceivedCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}

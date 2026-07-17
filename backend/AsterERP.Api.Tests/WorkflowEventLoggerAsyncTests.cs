using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.EventLogger;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowEventLoggerAsyncTests
{
    [Fact]
    public async Task EventLoggerListener_UsesAsyncLoggerContract()
    {
        var flusher = new CapturingFlusher();
        var eventLogger = new EventLogger(
            flusher,
            new EventLoggerConfiguration
            {
                FlushInterval = TimeSpan.Zero,
                BatchSize = 1
            });
        var listener = new EventLoggerListener(eventLogger, EventLoggerConfiguration.Default);
        var workflowEvent = WorkflowEventBuilder.CreateProcessStartedEvent("instance-1", "definition-1", null);

        await eventLogger.StartAsync();
        Assert.Throws<NotSupportedException>(() => listener.OnEvent(workflowEvent));

        using var cancellation = new CancellationTokenSource();
        await listener.OnEventAsync(workflowEvent, cancellation.Token);
        await eventLogger.StopAsync();

        var entry = Assert.Single(flusher.Entries);
        Assert.Equal(WorkflowEventType.PROCESS_STARTED.ToString(), entry.Type);
        Assert.Equal(cancellation.Token, flusher.CancellationToken);
    }

    [Fact]
    public async Task EventLogger_LogEventAsync_PropagatesCancellation()
    {
        var eventLogger = new EventLogger(
            new CapturingFlusher(),
            new EventLoggerConfiguration { FlushInterval = TimeSpan.Zero });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            eventLogger.LogEventAsync(new EventLogEntry { Type = "TEST" }, cancellation.Token));
    }

    private sealed class CapturingFlusher : IEventFlusher
    {
        public List<EventLogEntry> Entries { get; } = [];
        public CancellationToken CancellationToken { get; private set; }

        public Task FlushAsync(List<EventLogEntry> events, CancellationToken cancellationToken = default)
        {
            Entries.AddRange(events);
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

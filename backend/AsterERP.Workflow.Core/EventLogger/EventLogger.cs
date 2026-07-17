using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Core.EventLogger;

public interface IEventLogger
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task LogEventAsync(EventLogEntry entry, CancellationToken cancellationToken = default);
    Task LogEventAsync(object data, CancellationToken cancellationToken = default);
}

public class EventLogger : IEventLogger, IAsyncDisposable
{
    private readonly IEventFlusher _flusher;
    private readonly EventLoggerConfiguration _configuration;
    private readonly ILogger<EventLogger>? _logger;

    private readonly List<EventLogEntry> _buffer = new();
    private readonly object _bufferLock = new();
    private CancellationTokenSource? _flushCancellation;
    private Task? _flushTask;
    private bool _isRunning;
    private bool _isDisposed;

    public EventLogger(
        IEventFlusher flusher,
        EventLoggerConfiguration configuration,
        ILogger<EventLogger>? logger = null)
    {
        _flusher = flusher;
        _configuration = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_isRunning) return Task.CompletedTask;

        _isRunning = true;

        if (_configuration.FlushInterval > TimeSpan.Zero)
        {
            _flushCancellation = new CancellationTokenSource();
            _flushTask = RunFlushLoopAsync(_flushCancellation.Token);
        }

        _logger?.LogInformation("EventLogger started with flush interval {FlushInterval}ms, batch size {BatchSize}",
            _configuration.FlushInterval.TotalMilliseconds, _configuration.BatchSize);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning) return;

        _isRunning = false;

        _flushCancellation?.Cancel();
        if (_flushTask != null)
        {
            await _flushTask;
        }

        _flushCancellation?.Dispose();
        _flushCancellation = null;
        _flushTask = null;

        await FlushBufferAsync(cancellationToken);
        await _flusher.CloseAsync(cancellationToken);

        _logger?.LogInformation("EventLogger stopped");
    }

    public async Task LogEventAsync(EventLogEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_isRunning || _isDisposed) return;

        var shouldFlush = false;
        lock (_bufferLock)
        {
            _buffer.Add(entry);

            if (_buffer.Count >= _configuration.BatchSize)
            {
                shouldFlush = true;
            }
        }

        if (shouldFlush)
        {
            await FlushBufferAsync(cancellationToken);
        }
    }

    public Task LogEventAsync(object data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_isRunning || _isDisposed) return Task.CompletedTask;

        var entry = new EventLogEntry
        {
            Type = data.GetType().Name,
            Data = new Dictionary<string, object?>
            {
                ["Data"] = data
            }
        };

        return LogEventAsync(entry, cancellationToken);
    }

    private async Task RunFlushLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_configuration.FlushInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await FlushBufferAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task FlushBufferAsync(CancellationToken cancellationToken = default)
    {
        List<EventLogEntry> entriesToFlush;

        lock (_bufferLock)
        {
            if (_buffer.Count == 0) return;

            entriesToFlush = new List<EventLogEntry>(_buffer);
            _buffer.Clear();
        }

        try
        {
            await _flusher.FlushAsync(entriesToFlush, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (_bufferLock)
            {
                _buffer.InsertRange(0, entriesToFlush);
            }

            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to flush {Count} event log entries", entriesToFlush.Count);

            lock (_bufferLock)
            {
                _buffer.InsertRange(0, entriesToFlush);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        await StopAsync();
    }
}

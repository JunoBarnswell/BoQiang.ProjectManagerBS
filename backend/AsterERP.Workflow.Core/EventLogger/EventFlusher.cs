using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Core.EventLogger;

public interface IEventFlusher
{
    Task FlushAsync(List<EventLogEntry> events, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
}

public class ConsoleEventFlusher : IEventFlusher
{
    private readonly ILogger<ConsoleEventFlusher>? _logger;

    public ConsoleEventFlusher(ILogger<ConsoleEventFlusher>? logger = null)
    {
        _logger = logger;
    }

    public Task FlushAsync(List<EventLogEntry> events, CancellationToken cancellationToken = default)
    {
        foreach (var entry in events)
        {
            var message = $"[EventLog] {entry.TimeStamp:yyyy-MM-dd HH:mm:ss.fff} | {entry.Type} | " +
                          $"ProcessDefId={entry.ProcessDefinitionId} | ProcessInstId={entry.ProcessInstanceId} | " +
                          $"ExecutionId={entry.ExecutionId} | TaskId={entry.TaskId}";

            if (entry.Data != null && entry.Data.Count > 0)
            {
                var dataStr = string.Join(", ", entry.Data.Select(kv => $"{kv.Key}={kv.Value}"));
                message += $" | Data: {{{dataStr}}}";
            }

            _logger?.LogInformation(message);
        }

        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class DatabaseEventFlusher : IEventFlusher
{
    private readonly IEventLoggerRepository _repository;
    private readonly ILogger<DatabaseEventFlusher>? _logger;

    public DatabaseEventFlusher(IEventLoggerRepository repository, ILogger<DatabaseEventFlusher>? logger = null)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task FlushAsync(List<EventLogEntry> events, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0) return;

        try
        {
            await _repository.SaveEventLogEntriesAsync(events, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to flush event log entries to database");
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await _repository.CloseAsync(cancellationToken);
    }
}

public interface IEventLoggerRepository
{
    Task SaveEventLogEntriesAsync(List<EventLogEntry> entries, CancellationToken cancellationToken = default);
    Task<List<EventLogEntry>> GetEventLogEntriesAsync(string? processInstanceId = null, string? type = null, int maxResults = 100, CancellationToken cancellationToken = default);
    Task DeleteEventLogEntryAsync(string eventLogEntryId, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
}

public class InMemoryEventLoggerRepository : IEventLoggerRepository
{
    private readonly List<EventLogEntry> _entries = new();
    private readonly object _lock = new();

    public Task SaveEventLogEntriesAsync(List<EventLogEntry> entries, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _entries.AddRange(entries);
        }
        return Task.CompletedTask;
    }

    public Task<List<EventLogEntry>> GetEventLogEntriesAsync(string? processInstanceId = null, string? type = null, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IEnumerable<EventLogEntry> query = _entries;

            if (processInstanceId != null)
                query = query.Where(e => e.ProcessInstanceId == processInstanceId);

            if (type != null)
                query = query.Where(e => e.Type == type);

            return Task.FromResult(query.OrderByDescending(e => e.TimeStamp).Take(maxResults).ToList());
        }
    }

    public Task DeleteEventLogEntryAsync(string eventLogEntryId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _entries.RemoveAll(x => x.Id == eventLogEntryId);
        }

        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

using System.Collections.Concurrent;

namespace AsterERP.Api.Infrastructure.Ai;

public sealed class AiRunCancellationRegistry
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> sources = new(StringComparer.OrdinalIgnoreCase);

    public CancellationTokenSource CreateLinked(string runId, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        sources[runId] = source;
        return source;
    }

    public bool Cancel(string runId)
    {
        if (!sources.TryGetValue(runId, out var source))
        {
            return false;
        }

        source.Cancel();
        return true;
    }

    public void Complete(string runId)
    {
        if (sources.TryRemove(runId, out var source))
        {
            source.Dispose();
        }
    }
}

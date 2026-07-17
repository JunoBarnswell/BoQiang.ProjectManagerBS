using AsterERP.Api.Modules.System.Logs;

namespace AsterERP.Api.Infrastructure.Logging;

public interface IOperationLogQueue
{
    long DroppedCount { get; }

    long DroppedLowValueGetCount { get; }

    long OverflowDroppedCount { get; }

    bool TryEnqueue(SystemOperationLogEntity operationLog);

    bool TryRead(out SystemOperationLogEntity operationLog);

    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken);
}

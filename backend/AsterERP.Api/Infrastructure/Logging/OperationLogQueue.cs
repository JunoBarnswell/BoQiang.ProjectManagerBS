using System.Threading.Channels;
using AsterERP.Api.Modules.System.Logs;

namespace AsterERP.Api.Infrastructure.Logging;

public sealed class OperationLogQueue : IOperationLogQueue
{
    private readonly Channel<SystemOperationLogEntity> channel;
    private readonly ILogger<OperationLogQueue> logger;
    private long droppedLowValueGetCount;
    private long overflowDroppedCount;

    public OperationLogQueue(IConfiguration configuration, ILogger<OperationLogQueue> logger)
    {
        this.logger = logger;
        var capacity = Math.Clamp(configuration.GetValue("OperationLog:QueueCapacity", 2048), 128, 100_000);
        channel = Channel.CreateBounded<SystemOperationLogEntity>(new BoundedChannelOptions(capacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public long DroppedCount => DroppedLowValueGetCount + OverflowDroppedCount;

    public long DroppedLowValueGetCount => Interlocked.Read(ref droppedLowValueGetCount);

    public long OverflowDroppedCount => Interlocked.Read(ref overflowDroppedCount);

    public bool TryEnqueue(SystemOperationLogEntity operationLog)
    {
        if (channel.Writer.TryWrite(operationLog))
        {
            return true;
        }

        var isLowValueGet = IsLowValueGet(operationLog);
        var currentDroppedCount = isLowValueGet
            ? Interlocked.Increment(ref droppedLowValueGetCount)
            : Interlocked.Increment(ref overflowDroppedCount);
        if (currentDroppedCount == 1 || currentDroppedCount % 100 == 0)
        {
            logger.LogWarning(
                "Operation log queue is full; dropped {DropKind} operation log count={DroppedCount} latest={Method} {Path} {StatusCode}",
                isLowValueGet ? "low-value GET" : "overflow",
                currentDroppedCount,
                operationLog.RequestMethod,
                operationLog.RequestPath,
                operationLog.StatusCode);
        }

        return false;
    }

    public bool TryRead(out SystemOperationLogEntity operationLog)
    {
        var success = channel.Reader.TryRead(out var item);
        operationLog = item!;
        return success;
    }

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) =>
        channel.Reader.WaitToReadAsync(cancellationToken);

    private static bool IsLowValueGet(SystemOperationLogEntity operationLog) =>
        string.Equals(operationLog.RequestMethod, "GET", StringComparison.OrdinalIgnoreCase) &&
        operationLog.StatusCode is >= 200 and < 500;
}

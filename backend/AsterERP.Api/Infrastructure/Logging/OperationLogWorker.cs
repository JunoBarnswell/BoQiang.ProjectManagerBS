using AsterERP.Api.Modules.System.Logs;

namespace AsterERP.Api.Infrastructure.Logging;

public sealed class OperationLogWorker(
    IOperationLogQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<OperationLogWorker> logger) : BackgroundService
{
    private const int MaxBatchSize = 32;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await queue.WaitToReadAsync(stoppingToken))
                {
                    continue;
                }

                var batch = DrainBatch();
                if (batch.Count > 0)
                {
                    await WriteBatchAsync(batch, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Operation log worker failed while consuming queued logs");
            }
        }
    }

    private List<SystemOperationLogEntity> DrainBatch()
    {
        var batch = new List<SystemOperationLogEntity>(MaxBatchSize);
        while (batch.Count < MaxBatchSize && queue.TryRead(out var operationLog))
        {
            batch.Add(operationLog);
        }

        return batch;
    }

    private async Task WriteBatchAsync(IReadOnlyList<SystemOperationLogEntity> batch, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var operationLogWriter = scope.ServiceProvider.GetRequiredService<IOperationLogWriter>();

        try
        {
            await operationLogWriter.WriteBatchAsync(batch, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write operation log batch count={Count}", batch.Count);
        }
    }
}

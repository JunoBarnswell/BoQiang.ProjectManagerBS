using AsterERP.Api.Modules.System.Logs;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Logging;

public sealed class OperationLogWriter(
    ISqlSugarClient db,
    ILogger<OperationLogWriter> logger) : IOperationLogWriter
{
    public async Task WriteAsync(SystemOperationLogEntity operationLog, CancellationToken cancellationToken = default)
    {
        NormalizeForInsert(operationLog);
        await db.Insertable(operationLog).ExecuteCommandAsync(cancellationToken);
        logger.LogInformation(
            "Operation logged: {Method} {Path} -> {StatusCode} ({TraceId})",
            operationLog.RequestMethod,
            operationLog.RequestPath,
            operationLog.StatusCode,
            operationLog.TraceId);
    }

    public async Task WriteBatchAsync(IReadOnlyList<SystemOperationLogEntity> operationLogs, CancellationToken cancellationToken = default)
    {
        if (operationLogs.Count == 0)
        {
            return;
        }

        foreach (var operationLog in operationLogs)
        {
            NormalizeForInsert(operationLog);
        }

        await db.Insertable(operationLogs.ToList()).ExecuteCommandAsync(cancellationToken);
        logger.LogInformation("Operation log batch written: count={Count}", operationLogs.Count);
    }

    private static void NormalizeForInsert(SystemOperationLogEntity operationLog)
    {
        operationLog.CreatedTime = operationLog.CreatedTime == default ? DateTime.UtcNow : operationLog.CreatedTime;
        operationLog.IsDeleted = false;
    }
}

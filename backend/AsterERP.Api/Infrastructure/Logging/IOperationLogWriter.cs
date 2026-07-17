using AsterERP.Api.Modules.System.Logs;

namespace AsterERP.Api.Infrastructure.Logging;

public interface IOperationLogWriter
{
    Task WriteAsync(SystemOperationLogEntity operationLog, CancellationToken cancellationToken = default);

    Task WriteBatchAsync(IReadOnlyList<SystemOperationLogEntity> operationLogs, CancellationToken cancellationToken = default);
}

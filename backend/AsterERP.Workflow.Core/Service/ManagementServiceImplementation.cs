using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Core.EventLogger;

namespace AsterERP.Workflow.Core.Service;

public class ManagementServiceImplementation : ServiceImpl, IManagementService
{
    private readonly IEventLoggerRepository? _eventLoggerRepository;

    public ManagementServiceImplementation() { }

    public ManagementServiceImplementation(IProcessEngineConfiguration processEngineConfiguration)
        : base(processEngineConfiguration) { }

    public ManagementServiceImplementation(ICommandExecutor commandExecutor)
        : base(commandExecutor) { }

    public ManagementServiceImplementation(IProcessEngineConfiguration processEngineConfiguration, IEventLoggerRepository eventLoggerRepository)
        : base(processEngineConfiguration)
    {
        _eventLoggerRepository = eventLoggerRepository;
    }

    public ManagementServiceImplementation(ICommandExecutor commandExecutor, IEventLoggerRepository eventLoggerRepository)
        : base(commandExecutor)
    {
        _eventLoggerRepository = eventLoggerRepository;
    }

    public async Task<Dictionary<string, long>> GetTableCountAsync(CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTableCountCmd(), cancellationToken);
    }

    public async Task<string> GetTableNameAsync(Type entityClass, CancellationToken cancellationToken = default)
    {
        if (entityClass == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("entityClass is null");

        return await CommandExecutor.ExecuteAsync(new GetTableNameCmd(entityClass), cancellationToken);
    }

    public async Task<Services.TableMetaData?> GetTableMetaDataAsync(string tableName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("tableName is null");

        var result = await CommandExecutor.ExecuteAsync(new GetTableMetaDataCmd(tableName.Trim()), cancellationToken);
        if (result == null)
        {
            return null;
        }

        var columns = result.Columns ?? [];

        return new Services.TableMetaData
        {
            Name = result.Name,
            ColumnNames = columns.Select(c => c.Name).ToList(),
            ColumnTypes = columns.Select(c => c.Type).ToList()
        };
    }

    public async Task<Dictionary<string, string>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetPropertiesCmd(), cancellationToken);
    }

    public async Task<long> GetNextIdBlockAsync(CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetNextIdBlockCmd(), cancellationToken);
    }

    public T ExecuteCommand<T>(ICommand<T> command)
    {
        EnsureCommandNotNull(command);
        return CommandExecutor.Execute(command);
    }

    public async Task<T> ExecuteCommandAsync<T>(ICommand<T> command, CancellationToken cancellationToken = default)
    {
        EnsureCommandNotNull(command);
        return await CommandExecutor.ExecuteAsync(command, cancellationToken);
    }

    public async Task<object?> ExecuteCustomSqlAsync(ICustomSqlExecution<object> customSqlExecution, CancellationToken cancellationToken = default)
    {
        if (customSqlExecution == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("customSqlExecution is null");
        return await CommandExecutor.ExecuteAsync(new ExecuteCustomSqlCmd(customSqlExecution), cancellationToken);
    }

    public async Task ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");
        await CommandExecutor.ExecuteAsync(new ExecuteJobCmd(jobId), cancellationToken);
    }

    public async Task<JobEntity?> MoveTimerToExecutableJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");

        var movedJob = await CommandExecutor.ExecuteAsync(new MoveTimerToExecutableJobCmd(jobId), cancellationToken);
        if (movedJob != null && movedJob.Retries <= 0)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("moved executable job retries must be greater than 0");
        if (movedJob != null && !string.Equals(movedJob.Id, jobId, StringComparison.Ordinal))
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Moved executable job id does not match requested timer job id.");

        return movedJob;
    }

    public async Task MoveJobToDeadLetterJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");

        await CommandExecutor.ExecuteAsync(new MoveJobToDeadLetterJobCmd(jobId), cancellationToken);
    }

    public async Task<JobEntity?> MoveDeadLetterJobToExecutableJobAsync(string jobId, int retries, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");
        if (retries <= 0)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("retries must be greater than 0");

        return await CommandExecutor.ExecuteAsync(new MoveDeadLetterJobToExecutableJobCmd(jobId, retries), cancellationToken);
    }

    public async Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");

        await CommandExecutor.ExecuteAsync(new DeleteJobCmd(jobId), cancellationToken);
    }

    public async Task DeleteTimerJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");

        await CommandExecutor.ExecuteAsync(new DeleteTimerJobCmd(jobId), cancellationToken);
    }

    public async Task DeleteDeadLetterJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");

        await CommandExecutor.ExecuteAsync(new DeleteDeadLetterJobCmd(jobId), cancellationToken);
    }

    public async Task SetJobRetriesAsync(string jobId, int retries, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");

        await CommandExecutor.ExecuteAsync(new SetJobRetriesCmd(jobId, retries), cancellationToken);
    }

    public async Task SetTimerJobRetriesAsync(string jobId, int retries, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");

        await CommandExecutor.ExecuteAsync(new SetTimerJobRetriesCmd(jobId, retries), cancellationToken);
    }

    public async Task<string?> GetJobExceptionStacktraceAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");

        return await CommandExecutor.ExecuteAsync(new GetJobExceptionStacktraceCmd(jobId, "async"), cancellationToken);
    }

    public async Task<string?> GetTimerJobExceptionStacktraceAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");

        return await CommandExecutor.ExecuteAsync(new GetJobExceptionStacktraceCmd(jobId, "timer"), cancellationToken);
    }

    public async Task<string?> GetDeadLetterJobExceptionStacktraceAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("jobId is null");

        return await CommandExecutor.ExecuteAsync(new GetJobExceptionStacktraceCmd(jobId, "deadletter"), cancellationToken);
    }

    public async Task<List<AsterERP.Workflow.Core.EventLogger.EventLogEntry>> GetEventLogEntriesAsync(long startLogNr, int pageSize, CancellationToken cancellationToken = default)
    {
        if (startLogNr < 0)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("startLogNr must be 0 or larger");
        if (startLogNr > int.MaxValue)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException($"startLogNr must be less than or equal to {int.MaxValue}");
        if (pageSize <= 0)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("pageSize must be larger than 0");
        if (_eventLoggerRepository == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Event logger repository is not configured.");

        var cappedStart = (int)startLogNr;
        var fetchSize = cappedStart + pageSize;
        var entries = await _eventLoggerRepository.GetEventLogEntriesAsync(maxResults: fetchSize, cancellationToken: cancellationToken);
        return entries.Skip(cappedStart).Take(pageSize).ToList();
    }

    public async Task DeleteEventLogEntryAsync(string eventLogEntryId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventLogEntryId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("eventLogEntryId is null");
        if (_eventLoggerRepository == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Event logger repository is not configured.");

        await _eventLoggerRepository.DeleteEventLogEntryAsync(eventLogEntryId, cancellationToken);
    }

    public Task<JobRecord?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return GetJobInternalAsync(new GetJobCmd(jobId, CmdJobType.AsyncContinuation), cancellationToken);
    }

    public Task<List<string>> GetTableListAsync(CancellationToken cancellationToken = default)
    {
        return GetKnownTableNamesInternal(cancellationToken);
    }

    public Task<Services.TablePage> GetTablePageAsync(string tableName, int firstResult, int maxResults, CancellationToken cancellationToken = default)
    {
        return GetTablePageAsyncInternal(tableName, firstResult, maxResults, cancellationToken);
    }

    public Task<JobRecord?> GetTimerJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return GetJobInternalAsync(new GetJobCmd(jobId, CmdJobType.Timer), cancellationToken);
    }

    public Task<JobRecord?> GetDeadLetterJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return GetJobInternalAsync(new GetJobCmd(jobId, CmdJobType.DeadLetter), cancellationToken);
    }

    private async Task<JobRecord?> GetJobInternalAsync(GetJobCmd command, CancellationToken cancellationToken)
    {
        try
        {
            return await CommandExecutor.ExecuteAsync(command, cancellationToken);
        }
        catch (AsterERP.Workflow.Common.WorkflowEngineObjectNotFoundException)
        {
            return null;
        }
    }

    private async Task<List<string>> GetKnownTableNamesInternal(CancellationToken cancellationToken)
    {
        var counts = await GetTableCountAsync(cancellationToken);
        return counts.Keys.OrderBy(x => x).ToList();
    }

    private async Task<Services.TablePage> GetTablePageAsyncInternal(string tableName, int firstResult, int maxResults, CancellationToken cancellationToken)
    {
        return await CommandExecutor.ExecuteAsync(new GetTablePageCmd(tableName, firstResult, maxResults), cancellationToken);
    }

    private static void EnsureCommandNotNull<T>(ICommand<T> command)
    {
        if (command == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("The command is null");
    }

}

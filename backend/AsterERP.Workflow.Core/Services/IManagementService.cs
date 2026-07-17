using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Job;

namespace AsterERP.Workflow.Core.Services;

public interface IManagementService
{
    T ExecuteCommand<T>(Core.Command.ICommand<T> command);
    Task<T> ExecuteCommandAsync<T>(Core.Command.ICommand<T> command, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetPropertiesAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetTableListAsync(CancellationToken cancellationToken = default);
    Task<TableMetaData?> GetTableMetaDataAsync(string tableName, CancellationToken cancellationToken = default);
    Task<TablePage> GetTablePageAsync(string tableName, int firstResult, int maxResults, CancellationToken cancellationToken = default);
    Task<JobRecord?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task SetJobRetriesAsync(string jobId, int retries, CancellationToken cancellationToken = default);
    Task<JobRecord?> GetTimerJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<JobRecord?> GetDeadLetterJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, long>> GetTableCountAsync(CancellationToken cancellationToken = default);
    Task<string> GetTableNameAsync(Type entityClass, CancellationToken cancellationToken = default);
    Task<long> GetNextIdBlockAsync(CancellationToken cancellationToken = default);
    Task DeleteTimerJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task DeleteDeadLetterJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task SetTimerJobRetriesAsync(string jobId, int retries, CancellationToken cancellationToken = default);
    Task<JobEntity?> MoveTimerToExecutableJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task MoveJobToDeadLetterJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<JobEntity?> MoveDeadLetterJobToExecutableJobAsync(string jobId, int retries, CancellationToken cancellationToken = default);
    Task<string?> GetJobExceptionStacktraceAsync(string jobId, CancellationToken cancellationToken = default);
    Task<string?> GetTimerJobExceptionStacktraceAsync(string jobId, CancellationToken cancellationToken = default);
    Task<string?> GetDeadLetterJobExceptionStacktraceAsync(string jobId, CancellationToken cancellationToken = default);
}

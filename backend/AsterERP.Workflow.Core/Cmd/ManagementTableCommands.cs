using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Management;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

internal static class ManagementTableMetadata
{
    public const string RuntimeJobTable = "ACT_RU_JOB";
    public const string RuntimeTimerJobTable = "ACT_RU_TIMER_JOB";
    public const string RuntimeDeadLetterJobTable = "ACT_RU_DEADLETTER_JOB";

    private static readonly IReadOnlyDictionary<string, TableDefinition> TableDefinitions = new Dictionary<string, TableDefinition>(StringComparer.Ordinal)
    {
        {
            RuntimeJobTable,
            new TableDefinition(
                RuntimeJobTable,
                [
                    "ID_", "TYPE_", "RETRIES_", "PROCESS_INSTANCE_ID_",
                    "PROCESS_DEFINITION_ID_", "EXCEPTION_MSG_", "DUEDATE_",
                    "HANDLER_TYPE_", "TENANT_ID_"
                ],
                [
                    "VARCHAR", "VARCHAR", "INTEGER", "VARCHAR", "VARCHAR",
                    "VARCHAR", "TIMESTAMP", "VARCHAR", "VARCHAR"
                ])
        },
        {
            RuntimeTimerJobTable,
            new TableDefinition(
                RuntimeTimerJobTable,
                [
                    "ID_", "DUEDATE_", "RETRIES_", "EXCEPTION_MSG_", "PROCESS_INSTANCE_ID_",
                    "HANDLER_TYPE_", "TENANT_ID_"
                ],
                [
                    "VARCHAR", "TIMESTAMP", "INTEGER", "VARCHAR", "VARCHAR",
                    "VARCHAR", "VARCHAR"
                ])
        },
        {
            RuntimeDeadLetterJobTable,
            new TableDefinition(
                RuntimeDeadLetterJobTable,
                [
                    "ID_", "EXCEPTION_MSG_", "DUEDATE_", "RETRIES_", "PROCESS_INSTANCE_ID_",
                    "HANDLER_TYPE_", "TENANT_ID_"
                ],
                [
                    "VARCHAR", "VARCHAR", "TIMESTAMP", "INTEGER", "VARCHAR",
                    "VARCHAR", "VARCHAR"
                ])
        }
    };

    public static bool TryGetDefinition(string tableName, out TableDefinition definition)
    {
        return TableDefinitions.TryGetValue(tableName, out definition);
    }

    public static IReadOnlyList<TableColumnMetaData> GetColumns(string tableName)
    {
        if (!TryGetDefinition(tableName, out var definition))
            return [];

        return definition.ColumnNames
            .Zip(definition.ColumnTypes, (name, type) => new TableColumnMetaData
            {
                Name = name,
                Type = type
            })
            .ToList();
    }

    public static long GetRowCount(string tableName, int rowCount)
    {
        return tableName switch
        {
            RuntimeJobTable => rowCount,
            RuntimeTimerJobTable => rowCount,
            RuntimeDeadLetterJobTable => rowCount,
            _ => 0
        };
    }

    public static async Task<long> GetRowCountAsync(
        string tableName,
        IJobLifecycleManager lifecycleManager,
        CancellationToken cancellationToken = default)
    {
        switch (tableName)
        {
            case RuntimeJobTable:
                return (await lifecycleManager.GetJobsAsync(cancellationToken)).Count;
            case RuntimeTimerJobTable:
                return (await lifecycleManager.GetTimerJobsAsync(cancellationToken)).Count;
            case RuntimeDeadLetterJobTable:
                return (await lifecycleManager.GetDeadLetterJobsAsync(cancellationToken)).Count;
            default:
                return 0;
        }
    }

    public static async Task<IReadOnlyList<Dictionary<string, object?>>> GetRowsAsync(
        string tableName,
        IJobLifecycleManager lifecycleManager,
        CancellationToken cancellationToken = default)
    {
        return tableName switch
        {
            RuntimeJobTable => (await lifecycleManager.GetJobsAsync(cancellationToken))
                .Select(CreateRow)
                .ToList(),
            RuntimeTimerJobTable => (await lifecycleManager.GetTimerJobsAsync(cancellationToken))
                .Select(CreateRow)
                .ToList(),
            RuntimeDeadLetterJobTable => (await lifecycleManager.GetDeadLetterJobsAsync(cancellationToken))
                .Select(CreateRow)
                .ToList(),
            _ => []
        };
    }

    private static Dictionary<string, object?> CreateRow(JobEntity job)
    {
        return new Dictionary<string, object?>
        {
            ["ID_"] = job.Id,
            ["TYPE_"] = job.JobType,
            ["RETRIES_"] = job.Retries,
            ["PROCESS_INSTANCE_ID_"] = job.ProcessInstanceId,
            ["PROCESS_DEFINITION_ID_"] = job.ProcessDefinitionId,
            ["EXCEPTION_MSG_"] = job.ExceptionMessage,
            ["DUEDATE_"] = job.DueDate,
            ["HANDLER_TYPE_"] = job.HandlerType,
            ["TENANT_ID_"] = job.TenantId,
            ["EXECUTION_ID_"] = job.ExecutionId
        };
    }

    private static Dictionary<string, object?> CreateRow(TimerJobEntity job)
    {
        return new Dictionary<string, object?>
        {
            ["ID_"] = job.Id,
            ["TYPE_"] = job.JobType,
            ["RETRIES_"] = job.Retries,
            ["PROCESS_INSTANCE_ID_"] = job.ProcessInstanceId,
            ["PROCESS_DEFINITION_ID_"] = job.ProcessDefinitionId,
            ["EXCEPTION_MSG_"] = job.ExceptionMessage,
            ["DUEDATE_"] = job.DueDate,
            ["HANDLER_TYPE_"] = job.HandlerType,
            ["TENANT_ID_"] = job.TenantId,
            ["EXECUTION_ID_"] = job.ExecutionId
        };
    }

    internal readonly record struct TableDefinition(string Name, string[] ColumnNames, string[] ColumnTypes);
}

public class GetTablePageCmd : ICommand<Services.TablePage>
{
    private readonly string _tableName;
    private readonly int _firstResult;
    private readonly int _maxResults;

    public GetTablePageCmd(string tableName, int firstResult, int maxResults)
    {
        _tableName = tableName;
        _firstResult = firstResult;
        _maxResults = maxResults;
    }


    public async Task<Services.TablePage> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_tableName))
            throw new WorkflowEngineArgumentException("tableName is null");
        if (_firstResult < 0)
            throw new WorkflowEngineArgumentException("firstResult must be 0 or larger");
        if (_maxResults < 0)
            throw new WorkflowEngineArgumentException("maxResults must be 0 or larger");
        if (!ManagementTableMetadata.TryGetDefinition(_tableName, out _))
            throw new WorkflowEngineObjectNotFoundException($"No table metadata found for table '{_tableName}'.");

        var manager = context.ProcessEngineConfiguration.JobManager as IJobLifecycleManager;
        if (manager == null)
            throw new WorkflowEngineException("Process engine job manager does not support lifecycle table queries.");

        var allRows = await ManagementTableMetadata.GetRowsAsync(_tableName, manager, cancellationToken);

        if (_maxResults == 0)
            return new Services.TablePage
            {
                Rows = new List<Dictionary<string, object?>>(),
                TotalCount = allRows.Count
            };

        var rows = allRows
            .Skip(_firstResult)
            .Take(_maxResults)
            .ToList();

        return new Services.TablePage
        {
            Rows = rows,
            TotalCount = allRows.Count
        };
    }
}

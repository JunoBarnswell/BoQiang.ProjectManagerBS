using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Management;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Core.Variable;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;
using TableMetaData = AsterERP.Workflow.Core.Management.TableMetaData;

namespace AsterERP.Workflow.Core.Cmd;

public class SetDeploymentCategoryCmd : ICommand<object?>
{
    private readonly string _deploymentId;
    private readonly string? _category;

    public SetDeploymentCategoryCmd(string deploymentId, string? category)
    {
        _deploymentId = deploymentId;
        _category = category;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("SetDeploymentCategoryCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deployment = context.ProcessEngineConfiguration.DeploymentManager?.FindDeploymentById(_deploymentId)
            ?? throw new WorkflowEngineObjectNotFoundException($"No deployment found for id = '{_deploymentId}'", typeof(Deploy.DeploymentEntity));
        deployment.Category = _category;
        var dispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (dispatcher.IsEnabled) await dispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_UPDATED, deployment), cancellationToken);
        return null;
    }
}

public class SetDeploymentKeyCmd : ICommand<object?>
{
    private readonly string _deploymentId;
    private readonly string? _key;

    public SetDeploymentKeyCmd(string deploymentId, string? key)
    {
        _deploymentId = deploymentId;
        _key = key;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("SetDeploymentKeyCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deployment = context.ProcessEngineConfiguration.DeploymentManager?.FindDeploymentById(_deploymentId)
            ?? throw new WorkflowEngineObjectNotFoundException($"No deployment found for id = '{_deploymentId}'", typeof(Deploy.DeploymentEntity));
        deployment.Key = _key;
        var dispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (dispatcher.IsEnabled) await dispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_UPDATED, deployment), cancellationToken);
        return null;
    }
}

public class ChangeDeploymentTenantIdCmd : ICommand<object?>
{
    private readonly string _deploymentId;
    private readonly string _newTenantId;

    public ChangeDeploymentTenantIdCmd(string deploymentId, string newTenantId)
    {
        _deploymentId = deploymentId;
        _newTenantId = newTenantId;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("ChangeDeploymentTenantIdCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deployment = context.ProcessEngineConfiguration.DeploymentManager?.FindDeploymentById(_deploymentId)
            ?? throw new WorkflowEngineObjectNotFoundException($"No deployment found for id = '{_deploymentId}'", typeof(Deploy.DeploymentEntity));
        deployment.TenantId = _newTenantId;
        var dispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (dispatcher.IsEnabled) await dispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_UPDATED, deployment), cancellationToken);
        return null;
    }
}

public class GetTableNameCmd : ICommand<string>
{
    private readonly Type _entityClass;

    public GetTableNameCmd(Type entityClass)
    {
        _entityClass = entityClass ?? throw new WorkflowEngineArgumentException("entityClass is null");
    }

    public string Execute(ICommandContext context) => throw new NotSupportedException("GetTableNameCmd is async-only. Use ExecuteAsync.");

    public Task<string> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_entityClass.Name);
    }
}

public class GetTableMetaDataCmd : ICommand<TableMetaData?>
{
    private readonly string _tableName;

    public GetTableMetaDataCmd(string tableName)
    {
        _tableName = tableName;
    }


    public async Task<TableMetaData?> ExecuteAsync(
        ICommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_tableName))
            throw new WorkflowEngineArgumentException("tableName is null");

        if (!ManagementTableMetadata.TryGetDefinition(_tableName, out _))
            return null;

        var columns = ManagementTableMetadata.GetColumns(_tableName);
        var rowCount = context.ProcessEngineConfiguration.JobManager is IJobLifecycleManager manager
            ? await ManagementTableMetadata.GetRowCountAsync(_tableName, manager, cancellationToken)
            : 0;

        return new TableMetaData
        {
            Name = _tableName,
            RowCount = rowCount,
            Columns = columns.Select(column => new TableColumnMetaData
            {
                Name = column.Name,
                Type = column.Type,
                IsNullable = false,
                IsPrimaryKey = column.Name == "ID_"
            }).ToList()
        };
    }

}

public class GetTableCountCmd : ICommand<Dictionary<string, long>>
{

    public async Task<Dictionary<string, long>> ExecuteAsync(
        ICommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ProcessEngineConfiguration.JobManager is not IJobLifecycleManager manager)
            return new Dictionary<string, long>(StringComparer.Ordinal);

        return new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [ManagementTableMetadata.RuntimeJobTable] = await ManagementTableMetadata.GetRowCountAsync(
                ManagementTableMetadata.RuntimeJobTable, manager, cancellationToken),
            [ManagementTableMetadata.RuntimeTimerJobTable] = await ManagementTableMetadata.GetRowCountAsync(
                ManagementTableMetadata.RuntimeTimerJobTable, manager, cancellationToken),
            [ManagementTableMetadata.RuntimeDeadLetterJobTable] = await ManagementTableMetadata.GetRowCountAsync(
                ManagementTableMetadata.RuntimeDeadLetterJobTable, manager, cancellationToken)
        };
    }
}

public class GetPropertiesCmd : ICommand<Dictionary<string, string>>
{
    public Dictionary<string, string> Execute(ICommandContext context) => throw new NotSupportedException("GetPropertiesCmd is async-only. Use ExecuteAsync.");

    public Task<Dictionary<string, string>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = context.ProcessEngineConfiguration;
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["schema.version"] = "9.0.0",
            ["schema.history"] = "create(9.0.0)",
            ["next.dbid"] = GetNextIdBlockCmd.PeekNextId().ToString()
        };

        properties["database.schema.update"] = config.DatabaseSchemaUpdate ?? string.Empty;
        properties["database.type"] = config.DatabaseType ?? string.Empty;
        properties["database.datasource"] = config.DataSource ?? string.Empty;
        properties["history.level"] = config.HistoryLevel ?? string.Empty;
        properties["async.executor.enabled"] = config.IsAsyncExecutorEnabled ? "true" : "false";

        return Task.FromResult(properties);
    }
}

public class GetNextIdBlockCmd : ICommand<long>
{
    private const int IdBlockSize = 100;
    private static long _nextId = 1;

    public static long PeekNextId()
    {
        return System.Threading.Interlocked.Read(ref _nextId);
    }

    public long Execute(ICommandContext context) => throw new NotSupportedException("GetNextIdBlockCmd is async-only. Use ExecuteAsync.");

    public Task<long> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(System.Threading.Interlocked.Add(ref _nextId, IdBlockSize) - IdBlockSize);
    }
}

public class ValidateBpmnModelCmd : ICommand<List<ValidationError>>
{
    private readonly byte[]? _bpmnXml;

    public ValidateBpmnModelCmd(byte[]? bpmnXml)
    {
        _bpmnXml = bpmnXml;
    }

    public List<ValidationError> Execute(ICommandContext context) => throw new NotSupportedException("ValidateBpmnModelCmd is async-only. Use ExecuteAsync.");

    public Task<List<ValidationError>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<ValidationError>();

        if (_bpmnXml == null || _bpmnXml.Length == 0)
        {
            errors.Add(new ValidationError
            {
                Message = "BPMN XML is empty.",
                Type = "BPMN_VALIDATION",
                IsWarning = false
            });
            return Task.FromResult(errors);
        }

        try
        {
            var xml = Encoding.UTF8.GetString(_bpmnXml);
            var doc = XDocument.Parse(xml, LoadOptions.None);

            if (doc.Root == null)
            {
                errors.Add(new ValidationError
                {
                    Message = "BPMN XML root element is missing.",
                    Type = "BPMN_VALIDATION",
                    IsWarning = false
                });
                return Task.FromResult(errors);
            }

            var rootName = doc.Root.Name.LocalName;
            if (!string.Equals(rootName, "definitions", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Unexpected BPMN root element '{rootName}'. Expected 'definitions'.",
                    Type = "BPMN_VALIDATION",
                    IsWarning = false
                });
            }
        }
        catch (XmlException ex)
        {
            errors.Add(new ValidationError
            {
                Message = $"Invalid BPMN XML: {ex.Message}",
                Type = "BPMN_VALIDATION",
                IsWarning = false
            });
        }

        return Task.FromResult(errors);
    }
}

public class ValidationError
{
    public string? Id { get; set; }
    public string? ActivityId { get; set; }
    public string? Message { get; set; }
    public string? Type { get; set; }
    public bool IsWarning { get; set; }
}

public class ValidateExecutionRelatedEntityCountCfgCmd : ICommand<object?>
{

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var result = new ExecutionRelatedEntityCountValidationResult();

        if (context.ProcessEngineConfiguration.JobManager is not IJobLifecycleManager)
        {
            result.IsValid = false;
            result.Errors.Add("Job manager does not support lifecycle operations.");
            return result;
        }

        var tableCounts = await new GetTableCountCmd().ExecuteAsync(context, cancellationToken);
        result.TableCounts = tableCounts;

        if (!tableCounts.ContainsKey(ManagementTableMetadata.RuntimeJobTable))
            result.Errors.Add($"Missing table count for '{ManagementTableMetadata.RuntimeJobTable}'.");
        if (!tableCounts.ContainsKey(ManagementTableMetadata.RuntimeTimerJobTable))
            result.Errors.Add($"Missing table count for '{ManagementTableMetadata.RuntimeTimerJobTable}'.");
        if (!tableCounts.ContainsKey(ManagementTableMetadata.RuntimeDeadLetterJobTable))
            result.Errors.Add($"Missing table count for '{ManagementTableMetadata.RuntimeDeadLetterJobTable}'.");

        foreach (var kvp in tableCounts)
        {
            if (kvp.Value < 0)
                result.Errors.Add($"Table '{kvp.Key}' has negative count '{kvp.Value}'.");
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

}

public class ExecutionRelatedEntityCountValidationResult
{
    public bool IsValid { get; set; }
    public Dictionary<string, long> TableCounts { get; set; } = new(StringComparer.Ordinal);
    public List<string> Errors { get; set; } = new();
}

public interface ICustomSqlExecution<T>
{
    object? Execute(T mapper);
}

public abstract class AbstractCustomSqlExecution<T> : ICustomSqlExecution<T>
{
    public abstract object? Execute(T mapper);
}

public class ExecuteCustomSqlCmd : ICommand<object?>
{
    private readonly ICustomSqlExecution<object> _customSqlExecution;

    public ExecuteCustomSqlCmd(ICustomSqlExecution<object> customSqlExecution)
    {
        _customSqlExecution = customSqlExecution ?? throw new ArgumentNullException(nameof(customSqlExecution));
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("ExecuteCustomSqlCmd is async-only. Use ExecuteAsync.");

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context == null) throw new ArgumentNullException(nameof(context));
        return Task.FromResult(_customSqlExecution.Execute(context.ProcessEngineConfiguration));
    }
}

public class AddEventListenerCommand : ICommand<object?>
{
    private readonly IWorkflowEventListener _listener;

    public AddEventListenerCommand(IWorkflowEventListener listener)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("AddEventListenerCommand is async-only. Use ExecuteAsync.");

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.ProcessEngineConfiguration.EventDispatcher.AddEventListener(_listener);
        return Task.FromResult<object?>(null);
    }
}

public class RemoveEventListenerCommand : ICommand<object?>
{
    private readonly IWorkflowEventListener _listener;

    public RemoveEventListenerCommand(IWorkflowEventListener listener)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("RemoveEventListenerCommand is async-only. Use ExecuteAsync.");

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.ProcessEngineConfiguration.EventDispatcher.RemoveEventListener(_listener);
        return Task.FromResult<object?>(null);
    }
}

public class SaveProcessDefinitionInfoCmd : ICommand<object?>
{
    private readonly ProcessDefinitionInfoEntity _processDefinitionInfo;

    public SaveProcessDefinitionInfoCmd(ProcessDefinitionInfoEntity processDefinitionInfo)
    {
        _processDefinitionInfo = processDefinitionInfo ?? throw new ArgumentNullException(nameof(processDefinitionInfo));
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_processDefinitionInfo.ProcessDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");

        await ProcessDefinitionInfoCommandSupport.EnsurePersistenceInitializedAsync(context, cancellationToken);
        var db = ProcessDefinitionInfoCommandSupport.ResolveRequiredService<ISqlSugarClient>(context);
        var existing = db.Queryable<ProcessDefinitionInfoEntity>()
            .Where(item => item.ProcessDefinitionId == _processDefinitionInfo.ProcessDefinitionId)
            .First();

        var entity = existing ?? new ProcessDefinitionInfoEntity();
        entity.ProcessDefinitionId = _processDefinitionInfo.ProcessDefinitionId;
        entity.Revision = existing == null ? Math.Max(1, _processDefinitionInfo.Revision) : existing.Revision + 1;
        entity.InfoJson = _processDefinitionInfo.InfoJson;

        ProcessDefinitionInfoCommandSupport.PersistInfoJson(entity, existing, db);

        if (existing == null)
        {
            db.Insertable(entity).ExecuteCommand();
        }
        else
        {
            db.Updateable(entity).ExecuteCommand();
        }

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.ENTITY_UPDATED,
                    entity));
        }

        return null;
    }

}

public class GetProcessDefinitionInfoCmd : ICommand<ProcessDefinitionInfoEntity?>
{
    private readonly string _processDefinitionId;

    public GetProcessDefinitionInfoCmd(string processDefinitionId)
    {
        _processDefinitionId = processDefinitionId;
    }


    public async Task<ProcessDefinitionInfoEntity?> ExecuteAsync(
        ICommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_processDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");

        await ProcessDefinitionInfoCommandSupport.EnsurePersistenceInitializedAsync(context, cancellationToken);
        var db = ProcessDefinitionInfoCommandSupport.ResolveRequiredService<ISqlSugarClient>(context);
        var entity = db.Queryable<ProcessDefinitionInfoEntity>()
            .Where(item => item.ProcessDefinitionId == _processDefinitionId)
            .First();
        if (entity == null)
        {
            return null;
        }

        entity.InfoJson = ProcessDefinitionInfoCommandSupport.LoadInfoJson(entity.InfoJsonId, db);
        return entity;
    }

}

internal static class ProcessDefinitionInfoCommandSupport
{
    public static async Task EnsurePersistenceInitializedAsync(
        ICommandContext context,
        CancellationToken cancellationToken)
    {
        var store = ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(context.ProcessEngineConfiguration);
        if (store != null)
        {
            await store.InitializeAsync(context.ProcessEngineConfiguration, cancellationToken);
        }
    }

    public static void PersistInfoJson(
        ProcessDefinitionInfoEntity entity,
        ProcessDefinitionInfoEntity? existing,
        ISqlSugarClient db)
    {
        if (string.IsNullOrWhiteSpace(entity.InfoJson))
        {
            if (!string.IsNullOrWhiteSpace(existing?.InfoJsonId))
            {
                db.Deleteable<ByteArrayEntity>().In(existing.InfoJsonId).ExecuteCommand();
            }

            entity.InfoJsonId = null;
            return;
        }

        var infoJsonId = existing?.InfoJsonId;
        if (string.IsNullOrWhiteSpace(infoJsonId))
        {
            infoJsonId = AbpTimeIdProvider.NewGuid("N");
        }

        var bytes = Encoding.UTF8.GetBytes(entity.InfoJson);
        var byteArray = db.Queryable<ByteArrayEntity>().InSingle(infoJsonId);
        if (byteArray == null)
        {
            byteArray = new ByteArrayEntity
            {
                Id = infoJsonId,
                Name = $"{entity.ProcessDefinitionId}:info",
                Bytes = bytes,
                Generated = false
            };
            db.Insertable(byteArray).ExecuteCommand();
        }
        else
        {
            byteArray.Name = $"{entity.ProcessDefinitionId}:info";
            byteArray.Bytes = bytes;
            byteArray.Generated = false;
            db.Updateable(byteArray).ExecuteCommand();
        }

        entity.InfoJsonId = infoJsonId;
    }

    public static string? LoadInfoJson(string? infoJsonId, ISqlSugarClient db)
    {
        if (string.IsNullOrWhiteSpace(infoJsonId))
        {
            return null;
        }

        var byteArray = db.Queryable<ByteArrayEntity>().InSingle(infoJsonId);
        return byteArray?.Bytes == null ? null : Encoding.UTF8.GetString(byteArray.Bytes);
    }

    public static T ResolveRequiredService<T>(ICommandContext context) where T : notnull
    {
        return ProcessEngineServiceProviderAccessor.GetRequiredService<T>(context.ProcessEngineConfiguration);
    }
}

public class GetDataObjectCmd : ICommand<DataObjectImpl?>
{
    private readonly string _executionId;
    private readonly string _variableName;
    private readonly string? _locale;
    private readonly bool _withLocalizationFallback;

    public GetDataObjectCmd(string executionId, string variableName)
    {
        _executionId = executionId;
        _variableName = variableName;
    }

    public GetDataObjectCmd(string executionId, string variableName, string? locale, bool withLocalizationFallback)
    {
        _executionId = executionId;
        _variableName = variableName;
        _locale = locale;
        _withLocalizationFallback = withLocalizationFallback;
    }


    public async Task<DataObjectImpl?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");
        if (string.IsNullOrEmpty(_variableName))
            throw new WorkflowEngineArgumentException("variableName is null");

        try
        {
            var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
            if (execution == null)
                throw new WorkflowEngineObjectNotFoundException($"execution {_executionId} doesn't exist", typeof(Execution.ExecutionEntity));

            var value = execution.GetVariable(_variableName);
            if (value == null) return null;

            return new DataObjectImpl(
                _variableName,
                value,
                null,
                null,
                null,
                null,
                null);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

public class GetDataObjectsCmd : ICommand<Dictionary<string, DataObjectImpl>>
{
    private readonly string _executionId;
    private readonly ICollection<string>? _variableNames;
    private readonly string? _locale;
    private readonly bool _withLocalizationFallback;

    public GetDataObjectsCmd(string executionId, ICollection<string>? variableNames)
    {
        _executionId = executionId;
        _variableNames = variableNames;
    }

    public GetDataObjectsCmd(string executionId, ICollection<string>? variableNames, string? locale, bool withLocalizationFallback)
    {
        _executionId = executionId;
        _variableNames = variableNames;
        _locale = locale;
        _withLocalizationFallback = withLocalizationFallback;
    }


    public async Task<Dictionary<string, DataObjectImpl>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");

        var result = new Dictionary<string, DataObjectImpl>();

        try
        {
            var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
            if (execution == null)
                throw new WorkflowEngineObjectNotFoundException($"execution {_executionId} doesn't exist", typeof(Execution.ExecutionEntity));

            foreach (var kvp in execution.Variables)
            {
                if (_variableNames == null || _variableNames.Contains(kvp.Key))
                {
                    result[kvp.Key] = new DataObjectImpl(
                        kvp.Key,
                        kvp.Value,
                        null,
                        null,
                        null,
                        null,
                        null);
                }
            }
        }
        catch (ArgumentException)
        {
        }

        return result;
    }
}

public class DeploymentSettings
{
    public bool IsDuplicateFilterEnabled { get; set; }
    public bool IsDeployChangedOnly { get; set; }
}


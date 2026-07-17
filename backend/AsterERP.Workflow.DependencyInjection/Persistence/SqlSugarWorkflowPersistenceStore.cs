using System.Collections.Concurrent;
using System.Text;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.BpmnParser;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Deployer;
using AsterERP.Workflow.Core.Deploy;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.History;
using AsterERP.Workflow.Core.Service;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence;
using AsterERP.Workflow.Persistence.Database;
using AsterERP.Workflow.Persistence.Entities;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using CoreAttachmentEntity = AsterERP.Workflow.Core.Cmd.AttachmentEntity;
using CoreCommentEntity = AsterERP.Workflow.Core.Cmd.CommentEntity;
using CoreIdentityLinkEntity = AsterERP.Workflow.Core.Cmd.IdentityLinkEntity;
using CoreExecutionEntity = AsterERP.Workflow.Core.Execution.ExecutionEntity;
using CoreIntegrationContextEntity = AsterERP.Workflow.Core.Integration.IntegrationContextEntity;
using CoreTaskImplementation = AsterERP.Workflow.Core.Services.TaskImplementation;
using PersistentAttachmentEntity = AsterERP.Workflow.Persistence.Entities.AttachmentEntity;
using PersistentByteArrayEntity = AsterERP.Workflow.Persistence.Entities.ByteArrayEntity;
using PersistentCommentEntity = AsterERP.Workflow.Persistence.Entities.CommentEntity;
using PersistentDeploymentEntity = AsterERP.Workflow.Persistence.Entities.DeploymentEntity;
using PersistentExecutionEntity = AsterERP.Workflow.Persistence.Entities.ExecutionEntity;
using PersistentHistoricActivityEntity = AsterERP.Workflow.Persistence.Entities.HistoricActivityInstanceEntity;
using PersistentHistoricDetailEntity = AsterERP.Workflow.Persistence.Entities.HistoricDetailEntity;
using PersistentHistoricIdentityLinkEntity = AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity;
using PersistentHistoricProcessEntity = AsterERP.Workflow.Persistence.Entities.HistoricProcessInstanceEntity;
using PersistentHistoricTaskEntity = AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity;
using PersistentHistoricVariableEntity = AsterERP.Workflow.Persistence.Entities.HistoricVariableInstanceEntity;
using PersistentIdentityLinkEntity = AsterERP.Workflow.Persistence.Entities.IdentityLinkEntity;
using PersistentIntegrationContextEntity = AsterERP.Workflow.Persistence.Entities.IntegrationContextEntity;
using PersistentProcessDefinitionEntity = AsterERP.Workflow.Persistence.Entities.ProcessDefinitionEntity;
using PersistentResourceEntity = AsterERP.Workflow.Persistence.Entities.ResourceEntity;
using PersistentTaskEntity = AsterERP.Workflow.Persistence.Entities.TaskEntity;
using PersistentVariableEntity = AsterERP.Workflow.Persistence.Entities.VariableInstanceEntity;

namespace AsterERP.Workflow.DependencyInjection.Persistence;

public sealed class SqlSugarWorkflowPersistenceStore : IWorkflowPersistenceStore, IWorkflowSqlSugarClientAccessor
{
    private const string CandidateIdentityLinkType = "candidate";
    private const string AssigneeIdentityLinkType = "assignee";
    private const string OwnerIdentityLinkType = "owner";
    private const string StarterIdentityLinkType = "starter";
    private static readonly ConcurrentDictionary<string, bool> InitializedStores = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim InitializationGate = new(1, 1);

    private readonly ISqlSugarClient _db;
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly IServiceProvider _serviceProvider;

    public SqlSugarWorkflowPersistenceStore(
        ISqlSugarClient db,
        DatabaseInitializer databaseInitializer,
        IServiceProvider serviceProvider)
    {
        _db = db;
        _databaseInitializer = databaseInitializer;
        _serviceProvider = serviceProvider;
    }

    public bool IsEnabled => true;
    public bool HasActiveTransaction => _db.Ado.Transaction != null;
    public ISqlSugarClient Db => _db;

    public async Task InitializeAsync(IProcessEngineConfiguration processEngineConfiguration, CancellationToken cancellationToken = default)
    {
        var storeKey = ResolveStoreKey();
        if (InitializedStores.ContainsKey(storeKey))
        {
            return;
        }

        await InitializationGate.WaitAsync(cancellationToken);
        try
        {
            if (InitializedStores.ContainsKey(storeKey))
            {
                return;
            }

            _databaseInitializer.Initialize();
            await LoadDefinitionsAsync(processEngineConfiguration, cancellationToken);
            InitializedStores[storeKey] = true;
        }
        finally
        {
            InitializationGate.Release();
        }
    }

    private string ResolveStoreKey()
    {
        var connectionConfig = _db.CurrentConnectionConfig;
        return $"{connectionConfig.DbType}:{connectionConfig.ConnectionString}";
    }

    public async Task<string?> FindProcessInstanceIdByExecutionIdAsync(string executionId, CancellationToken cancellationToken = default)
    {
        var row = await _db.Queryable<PersistentExecutionEntity>().InSingleAsync(executionId);
        return row?.ProcessInstanceId ?? row?.Id;
    }

    public async Task<CoreExecutionEntity?> LoadExecutionTreeAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var executionRows = await _db.Queryable<PersistentExecutionEntity>()
            .Where(it => it.ProcessInstanceId == processInstanceId || it.Id == processInstanceId)
            .ToListAsync(cancellationToken);

        if (executionRows.Count == 0)
        {
            return null;
        }

        var taskRows = await _db.Queryable<PersistentTaskEntity>()
            .Where(it => it.ProcessInstanceId == processInstanceId)
            .ToListAsync(cancellationToken);

        var identityLinks = await _db.Queryable<PersistentIdentityLinkEntity>()
            .Where(it => it.ProcessInstanceId == processInstanceId)
            .ToListAsync(cancellationToken);

        var variableRows = await _db.Queryable<PersistentVariableEntity>()
            .Where(it => it.ProcessInstanceId == processInstanceId)
            .ToListAsync(cancellationToken);

        var executionMap = new Dictionary<string, CoreExecutionEntity>(StringComparer.Ordinal);
        var processEngineConfiguration = _serviceProvider.GetRequiredService<IProcessEngineConfiguration>();
        var processMap = SqlSugarWorkflowDefinitionCache.BuildProcessMap(processEngineConfiguration);
        var identityLinksByTaskId = GroupIdentityLinksByTaskId(identityLinks);

        foreach (var row in executionRows)
        {
            var execution = new CoreExecutionEntity
            {
                Id = row.Id,
                Revision = row.Revision,
                ProcessDefinitionId = row.ProcessDefinitionId,
                ProcessInstanceId = row.ProcessInstanceId ?? row.Id,
                ParentId = row.ParentId,
                SuperExecutionId = row.SuperExecutionId,
                CurrentActivityId = row.ActivityId,
                CurrentFlowElementId = row.ActivityId,
                ActivityId = row.ActivityId,
                IsActive = !row.IsEnded && row.IsActive && row.SuspensionState == 1,
                IsEnded = row.IsEnded,
                IsConcurrent = row.IsConcurrent,
                IsScope = row.IsScope,
                TenantId = row.TenantId,
                BusinessKey = row.BusinessKey,
                IsProcessInstanceType = string.Equals(row.ProcessInstanceId, row.Id, StringComparison.Ordinal) || row.ParentId == null
            };

            if (!string.IsNullOrWhiteSpace(row.ProcessDefinitionId) &&
                processMap.TryGetValue(row.ProcessDefinitionId, out var process))
            {
                execution.Process = process;
                execution.CurrentFlowElement = SqlSugarWorkflowDefinitionCache.FindFlowElement(process, row.ActivityId);
                execution.CurrentActivityName = execution.CurrentFlowElement?.Name;
            }

            executionMap[row.Id] = execution;
        }

        foreach (var execution in executionMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(execution.ParentId) &&
                executionMap.TryGetValue(execution.ParentId, out var parent))
            {
                execution.Parent = parent;
                if (!parent.ChildExecutions.Any(child => child.Id == execution.Id))
                {
                    parent.ChildExecutions.Add(execution);
                }
            }
        }

        var byteArrayMap = await LoadByteArraysAsync(variableRows, cancellationToken);

        foreach (var row in variableRows)
        {
            if (!string.IsNullOrWhiteSpace(row.ExecutionId) &&
                executionMap.TryGetValue(row.ExecutionId, out var execution) &&
                !string.IsNullOrWhiteSpace(row.Name))
            {
                execution.Variables[row.Name] = PersistenceVariableCodec.ReadValue(
                    row.Type,
                    row.TextValue,
                    row.TextValue2,
                    row.LongValue,
                    row.DoubleValue,
                    ResolveByteArrayContent(byteArrayMap, row.ByteArrayId));
            }
        }

        foreach (var taskRow in taskRows)
        {
            if (string.IsNullOrWhiteSpace(taskRow.ExecutionId) || !executionMap.TryGetValue(taskRow.ExecutionId, out var execution))
            {
                continue;
            }

            execution.TaskEntities.Add(SqlSugarWorkflowPersistenceMapper.MapTask(
                taskRow,
                identityLinksByTaskId.GetValueOrDefault(taskRow.Id, Array.Empty<PersistentIdentityLinkEntity>())));
        }

        return executionMap.Values.FirstOrDefault(e => e.IsProcessInstanceType)
            ?? executionMap.Values.FirstOrDefault(e => e.ParentId == null)
            ?? executionMap.Values.FirstOrDefault();
    }

    public async Task PersistRuntimeStateAsync(RuntimePersistenceBatch batch, CancellationToken cancellationToken = default)
    {
        var deletedProcessInstanceIds = batch.DeletedProcessInstanceIds
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (deletedProcessInstanceIds.Count > 0)
        {
            await DeleteProcessInstancesAsync(deletedProcessInstanceIds, cancellationToken);
        }

        if (batch.DeletedTaskIds.Count > 0)
        {
            await DeleteTasksByIdsAsync(batch.DeletedTaskIds, cancellationToken);
        }

        var activeRootExecutions = batch.RootExecutions
            .Where(rootExecution => !deletedProcessInstanceIds.Contains(rootExecution.ProcessInstanceId ?? rootExecution.Id))
            .DistinctBy(rootExecution => rootExecution.Id)
            .ToList();
        if (activeRootExecutions.Count > 0)
        {
            await PersistProcessInstanceStatesAsync(activeRootExecutions, cancellationToken);
        }

        var standaloneTaskRows = batch.StandaloneTasks
            .DistinctBy(task => task.Id)
            .Select(MapStandaloneTask)
            .ToList();
        if (standaloneTaskRows.Count > 0)
        {
            await PrepareTaskRevisionsAsync(standaloneTaskRows, cancellationToken);
            await UpsertBatchAsync(standaloneTaskRows, cancellationToken);
        }
    }

    public Task DeleteProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return DeleteProcessInstancesAsync(new[] { processInstanceId }, cancellationToken);
    }

    private async Task DeleteProcessInstancesAsync(IEnumerable<string> processInstanceIds, CancellationToken cancellationToken)
    {
        var ids = processInstanceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
        {
            return;
        }

        await _db.Deleteable<PersistentIdentityLinkEntity>().Where(it => ids.Contains(it.ProcessInstanceId!)).ExecuteCommandAsync(cancellationToken);
        await _db.Deleteable<PersistentVariableEntity>().Where(it => ids.Contains(it.ProcessInstanceId!)).ExecuteCommandAsync(cancellationToken);
        await _db.Deleteable<PersistentTaskEntity>().Where(it => ids.Contains(it.ProcessInstanceId!)).ExecuteCommandAsync(cancellationToken);
        await _db.Deleteable<EventSubscriptionEntity>().Where(it => ids.Contains(it.ProcessInstanceId!)).ExecuteCommandAsync(cancellationToken);
        await _db.Deleteable<PersistentExecutionEntity>()
            .Where(it => ids.Contains(it.Id) || ids.Contains(it.ProcessInstanceId!))
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task PersistProcessInstanceStatesAsync(IReadOnlyCollection<CoreExecutionEntity> rootExecutions, CancellationToken cancellationToken)
    {
        if (rootExecutions.Count == 0)
        {
            return;
        }

        var processInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        var currentExecutionIdsByProcess = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var currentTaskIdsByProcess = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var currentVariableIdsByProcess = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var currentIdentityLinkIdsByProcess = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var currentEventSubscriptionIdsByProcess = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        var allExecutionRows = new List<PersistentExecutionEntity>();
        var allTaskRows = new List<PersistentTaskEntity>();
        var allVariableMappings = new List<RuntimeVariableMapping>();
        var allIdentityLinkRows = new List<PersistentIdentityLinkEntity>();
        var allEventSubscriptions = new List<EventSubscriptionEntity>();

        foreach (var rootExecution in rootExecutions)
        {
            var processInstanceId = rootExecution.ProcessInstanceId ?? rootExecution.Id;
            processInstanceIds.Add(processInstanceId);

            var treeExecutions = FlattenExecutions(new[] { rootExecution }).DistinctBy(execution => execution.Id).ToList();

            var executionRows = treeExecutions.Select(MapRuntimeExecution).ToList();
            var taskRows = treeExecutions.SelectMany(MapRuntimeTasks).DistinctBy(task => task.Id).ToList();
            var variableMappings = treeExecutions.SelectMany(MapRuntimeVariables).DistinctBy(mapping => mapping.Row.Id).ToList();
            var variableRows = variableMappings.Select(mapping => mapping.Row).ToList();
            var identityLinkRows = treeExecutions.SelectMany(MapRuntimeIdentityLinks).DistinctBy(identityLink => identityLink.Id).ToList();
            var eventSubscriptions = treeExecutions.SelectMany(MapRuntimeEventSubscriptions).DistinctBy(subscription => subscription.Id).ToList();

            currentExecutionIdsByProcess[processInstanceId] = executionRows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
            currentTaskIdsByProcess[processInstanceId] = taskRows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
            currentVariableIdsByProcess[processInstanceId] = variableRows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
            currentIdentityLinkIdsByProcess[processInstanceId] = identityLinkRows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
            currentEventSubscriptionIdsByProcess[processInstanceId] = eventSubscriptions.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);

            allExecutionRows.AddRange(executionRows);
            allTaskRows.AddRange(taskRows);
            allVariableMappings.AddRange(variableMappings);
            allIdentityLinkRows.AddRange(identityLinkRows);
            allEventSubscriptions.AddRange(eventSubscriptions);
        }

        var processIds = processInstanceIds.ToList();
        var existingExecutions = await _db.Queryable<PersistentExecutionEntity>()
            .Where(it => processIds.Contains(it.Id) || processIds.Contains(it.ProcessInstanceId!))
            .Select(it => new { it.Id, it.ProcessInstanceId })
            .ToListAsync(cancellationToken);
        var executionIdsToDelete = existingExecutions
            .Where(row => !currentExecutionIdsByProcess.GetValueOrDefault(row.ProcessInstanceId ?? row.Id, new HashSet<string>(StringComparer.Ordinal)).Contains(row.Id))
            .Select(row => row.Id)
            .ToList();

        var existingTasks = await _db.Queryable<PersistentTaskEntity>()
            .Where(it => processIds.Contains(it.ProcessInstanceId!))
            .Select(it => new { it.Id, it.ProcessInstanceId })
            .ToListAsync(cancellationToken);
        var taskIdsToDelete = existingTasks
            .Where(row => !currentTaskIdsByProcess.GetValueOrDefault(row.ProcessInstanceId, new HashSet<string>(StringComparer.Ordinal)).Contains(row.Id))
            .Select(row => row.Id)
            .ToList();

        var existingVariables = await _db.Queryable<PersistentVariableEntity>()
            .Where(it => processIds.Contains(it.ProcessInstanceId!))
            .Select(it => new { it.Id, it.ProcessInstanceId })
            .ToListAsync(cancellationToken);
        var variableIdsToDelete = existingVariables
            .Where(row => !currentVariableIdsByProcess.GetValueOrDefault(row.ProcessInstanceId, new HashSet<string>(StringComparer.Ordinal)).Contains(row.Id))
            .Select(row => row.Id)
            .ToList();

        var existingIdentityLinks = await _db.Queryable<PersistentIdentityLinkEntity>()
            .Where(it => processIds.Contains(it.ProcessInstanceId!))
            .Select(it => new { it.Id, it.ProcessInstanceId })
            .ToListAsync(cancellationToken);
        var identityLinkIdsToDelete = existingIdentityLinks
            .Where(row => !currentIdentityLinkIdsByProcess.GetValueOrDefault(row.ProcessInstanceId, new HashSet<string>(StringComparer.Ordinal)).Contains(row.Id))
            .Select(row => row.Id)
            .ToList();

        var existingEventSubscriptions = await _db.Queryable<EventSubscriptionEntity>()
            .Where(it => processIds.Contains(it.ProcessInstanceId!))
            .Select(it => new { it.Id, it.ProcessInstanceId })
            .ToListAsync(cancellationToken);
        var eventSubscriptionIdsToDelete = existingEventSubscriptions
            .Where(row => !currentEventSubscriptionIdsByProcess.GetValueOrDefault(row.ProcessInstanceId, new HashSet<string>(StringComparer.Ordinal)).Contains(row.Id))
            .Select(row => row.Id)
            .ToList();

        await DeleteIdentityLinksByIdsAsync(identityLinkIdsToDelete, cancellationToken);
        await DeleteVariablesByIdsAsync(variableIdsToDelete, cancellationToken);
        await DeleteTasksByIdsAsync(taskIdsToDelete, cancellationToken);
        await DeleteEventSubscriptionsByIdsAsync(eventSubscriptionIdsToDelete, cancellationToken);
        await DeleteExecutionsByIdsAsync(executionIdsToDelete, cancellationToken);

        var executionRowsToUpsert = allExecutionRows
            .DistinctBy(row => row.Id)
            .ToList();
        await PersistExecutionsWithOptimisticLockAsync(executionRowsToUpsert, cancellationToken);

        var taskRowsToUpsert = allTaskRows
            .DistinctBy(row => row.Id)
            .ToList();
        await PrepareTaskRevisionsAsync(taskRowsToUpsert, cancellationToken);
        await UpsertBatchAsync(taskRowsToUpsert, cancellationToken);

        var byteArrayRows = allVariableMappings
            .Where(mapping => mapping.Bytes != null && !string.IsNullOrWhiteSpace(mapping.Row.ByteArrayId))
            .Select(mapping => new PersistentByteArrayEntity
            {
                Id = mapping.Row.ByteArrayId!,
                Name = mapping.Row.Name,
                Bytes = mapping.Bytes!,
                Generated = false
            })
            .DistinctBy(row => row.Id)
            .ToList();
        await UpsertBatchAsync(byteArrayRows, cancellationToken);

        var variableRowsToUpsert = allVariableMappings
            .Select(mapping => mapping.Row)
            .DistinctBy(row => row.Id)
            .ToList();
        await PrepareVariableRevisionsAsync(variableRowsToUpsert, cancellationToken);
        await UpsertBatchAsync(variableRowsToUpsert, cancellationToken);

        var identityLinkRowsToUpsert = allIdentityLinkRows
            .DistinctBy(row => row.Id)
            .ToList();
        await PrepareIdentityLinkRevisionsAsync(identityLinkRowsToUpsert, cancellationToken);
        await UpsertBatchAsync(identityLinkRowsToUpsert, cancellationToken);

        var eventSubscriptionsToUpsert = allEventSubscriptions
            .DistinctBy(row => row.Id)
            .ToList();
        await PrepareEventSubscriptionRevisionsAsync(eventSubscriptionsToUpsert, cancellationToken);
        await UpsertBatchAsync(eventSubscriptionsToUpsert, cancellationToken);
    }

    private Task DeleteExecutionsByIdsAsync(IEnumerable<string> executionIds, CancellationToken cancellationToken)
    {
        var ids = executionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Deleteable<PersistentExecutionEntity>().In(ids).ExecuteCommandAsync(cancellationToken);
    }

    private async Task DeleteTasksByIdsAsync(IEnumerable<string> taskIds, CancellationToken cancellationToken)
    {
        var ids = taskIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        await _db.Deleteable<PersistentIdentityLinkEntity>().Where(it => ids.Contains(it.TaskId!)).ExecuteCommandAsync(cancellationToken);
        await _db.Deleteable<PersistentVariableEntity>().Where(it => ids.Contains(it.TaskId!)).ExecuteCommandAsync(cancellationToken);
        await _db.Deleteable<PersistentTaskEntity>().In(ids).ExecuteCommandAsync(cancellationToken);
    }

    private async Task DeleteVariablesByIdsAsync(IEnumerable<string> variableIds, CancellationToken cancellationToken)
    {
        var ids = variableIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var byteArrayIds = await _db.Queryable<PersistentVariableEntity>()
            .In(ids)
            .Where(it => it.ByteArrayId != null)
            .Select(it => it.ByteArrayId!)
            .ToListAsync(cancellationToken);
        if (byteArrayIds.Count > 0)
        {
            await _db.Deleteable<PersistentByteArrayEntity>().In(byteArrayIds).ExecuteCommandAsync(cancellationToken);
        }

        await _db.Deleteable<PersistentVariableEntity>().In(ids).ExecuteCommandAsync(cancellationToken);
    }

    private Task DeleteIdentityLinksByIdsAsync(IEnumerable<string> identityLinkIds, CancellationToken cancellationToken)
    {
        var ids = identityLinkIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Deleteable<PersistentIdentityLinkEntity>().In(ids).ExecuteCommandAsync(cancellationToken);
    }

    private Task DeleteEventSubscriptionsByIdsAsync(IEnumerable<string> eventSubscriptionIds, CancellationToken cancellationToken)
    {
        var ids = eventSubscriptionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Deleteable<EventSubscriptionEntity>().In(ids).ExecuteCommandAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (System.Transactions.Transaction.Current == null)
        {
            await _db.Ado.BeginTranAsync();
        }
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (System.Transactions.Transaction.Current == null)
        {
            await _db.Ado.CommitTranAsync();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (System.Transactions.Transaction.Current == null)
        {
            await _db.Ado.RollbackTranAsync();
        }
    }

    public async Task PersistDeploymentAsync(
        DeploymentResult deployment,
        IReadOnlyDictionary<string, byte[]> resources,
        CancellationToken cancellationToken = default)
    {
        var deploymentEntity = new PersistentDeploymentEntity
        {
            Id = deployment.Id,
            Name = deployment.Name,
            Category = deployment.Category,
            TenantId = deployment.TenantId,
            DeployTime = deployment.DeploymentTime
        };

        await UpsertAsync(deploymentEntity, cancellationToken);

        var resourceEntities = resources
            .Select(resource => new PersistentResourceEntity
            {
                Id = $"{deployment.Id}:{resource.Key}",
                DeploymentId = deployment.Id,
                Name = resource.Key,
                Bytes = resource.Value,
                Generated = false
            })
            .ToList();
        if (resourceEntities.Count > 0)
        {
            await UpsertBatchAsync(resourceEntities, cancellationToken);
        }
    }

    public async Task PersistProcessDefinitionAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.BpmnModel bpmnModel,
        Process process,
        CancellationToken cancellationToken = default)
    {
        var resourceName = processDefinition.ResourceName;
        if (string.IsNullOrWhiteSpace(resourceName) && !string.IsNullOrWhiteSpace(processDefinition.DeploymentId))
        {
            resourceName = await _db.Queryable<PersistentResourceEntity>()
                .Where(it => it.DeploymentId == processDefinition.DeploymentId)
                .OrderBy(it => it.Name, OrderByType.Asc)
                .Select(it => it.Name)
                .FirstAsync(cancellationToken);
        }

        var persistentProcessDefinition = new PersistentProcessDefinitionEntity
        {
            Id = processDefinition.Id,
            Key = processDefinition.Key,
            Name = processDefinition.Name ?? process.Name,
            Category = processDefinition.Category,
            Description = processDefinition.Description,
            DeploymentId = processDefinition.DeploymentId,
            ResourceName = resourceName,
            Version = processDefinition.Version > 0
                ? processDefinition.Version
                : SqlSugarWorkflowDefinitionCache.ResolveDefinitionVersion(processDefinition.Id),
            SuspensionState = processDefinition.IsSuspended ? 2 : 1,
            TenantId = processDefinition.TenantId,
            DiagramResourceName = processDefinition.DiagramResourceName,
            HasStartFormKey = processDefinition.HasStartFormKey || process.FlowElements
                .OfType<StartEvent>()
                .Any(startEvent => !string.IsNullOrWhiteSpace(startEvent.FormKey)),
            HasGraphicalNotation = true
        };

        await UpsertAsync(persistentProcessDefinition, cancellationToken);
    }

    public async Task DeleteDeploymentAsync(string deploymentId, CancellationToken cancellationToken = default)
    {
        await _db.Deleteable<PersistentProcessDefinitionEntity>()
            .Where(it => it.DeploymentId == deploymentId)
            .ExecuteCommandAsync(cancellationToken);
        await _db.Deleteable<PersistentResourceEntity>()
            .Where(it => it.DeploymentId == deploymentId)
            .ExecuteCommandAsync(cancellationToken);
        await _db.Deleteable<PersistentDeploymentEntity>()
            .In(deploymentId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ExecutionRecord?> GetExecutionAsync(string executionId, CancellationToken cancellationToken = default)
    {
        return await LoadByIdAsync(
            _db.Queryable<PersistentExecutionEntity>(),
            executionId,
            SqlSugarWorkflowPersistenceMapper.MapExecution);
    }

    public async Task<List<ExecutionRecord>> GetExecutionsAsync(CancellationToken cancellationToken = default)
    {
        return await QueryOrderedAsync(
            _db.Queryable<PersistentExecutionEntity>(),
            query => query.OrderBy(it => it.Id, OrderByType.Asc),
            SqlSugarWorkflowPersistenceMapper.MapExecution,
            cancellationToken);
    }

    public async Task<List<ExecutionRecord>> GetExecutionsByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await QueryOrderedAsync(
            _db.Queryable<PersistentExecutionEntity>()
                .Where(it => it.ProcessInstanceId == processInstanceId || it.Id == processInstanceId),
            query => query.OrderBy(it => it.Id, OrderByType.Asc),
            SqlSugarWorkflowPersistenceMapper.MapExecution,
            cancellationToken);
    }

    public async Task<List<VariableInstanceRecord>> GetExecutionVariableInstancesAsync(string? executionId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<PersistentVariableEntity>();
        if (!string.IsNullOrWhiteSpace(executionId))
        {
            query = query.Where(it => it.ExecutionId == executionId);
        }

        return await QueryByteArrayBackedAsync(
            query,
            orderedQuery => orderedQuery.OrderBy(it => it.Name, OrderByType.Asc),
            entity => entity.ByteArrayId,
            (entity, bytes) => SqlSugarWorkflowPersistenceMapper.MapRuntimeVariableRecord(entity, bytes),
            cancellationToken);
    }

    public async Task<List<string>> FindExecutionIdsBySignalSubscriptionAsync(
        string signalName,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(signalName))
        {
            return new List<string>();
        }

        var query = _db.Queryable<EventSubscriptionEntity>()
            .Where(it =>
                it.EventType == SignalEventSubscriptionEntity.EventTypeSignal &&
                it.EventName == signalName &&
                it.ExecutionId != null);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            query = query.Where(it => it.TenantId == tenantId);
        }

        return await query
            .Select(it => it.ExecutionId!)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasMessageSubscriptionAsync(
        string executionId,
        string messageName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executionId) || string.IsNullOrWhiteSpace(messageName))
        {
            return false;
        }

        var count = await _db.Queryable<EventSubscriptionEntity>()
            .Where(it =>
                it.ExecutionId == executionId &&
                it.EventType == MessageEventSubscriptionEntity.EventTypeMessage &&
                it.EventName == messageName)
            .CountAsync(cancellationToken);

        return count > 0;
    }

    public async Task<CoreIntegrationContextEntity?> GetIntegrationContextAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await LoadByIdAsync(
            _db.Queryable<PersistentIntegrationContextEntity>(),
            id,
            MapIntegrationContext);
    }

    public Task UpsertIntegrationContextAsync(
        CoreIntegrationContextEntity integrationContext,
        CancellationToken cancellationToken = default)
    {
        var entity = new PersistentIntegrationContextEntity
        {
            Id = integrationContext.Id,
            ExecutionId = integrationContext.ExecutionId,
            ProcessInstanceId = integrationContext.ProcessInstanceId,
            ProcessDefinitionId = integrationContext.ProcessDefinitionId,
            FlowNodeId = integrationContext.FlowNodeId,
            ConnectorId = integrationContext.ConnectorId,
            CorrelationId = integrationContext.CorrelationId,
            Status = integrationContext.Status,
            ResultType = integrationContext.ResultType,
            CreatedDate = AbpTimeIdProvider.UtcNow
        };
        return UpsertAsync(entity, cancellationToken);
    }

    public Task DeleteIntegrationContextAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.CompletedTask;
        }

        return _db.Deleteable<PersistentIntegrationContextEntity>()
            .In(id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<CoreTaskImplementation?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await _db.Queryable<PersistentTaskEntity>().InSingleAsync(taskId);
        if (task == null)
        {
            return null;
        }

        return (await MapTasksAsync([task], cancellationToken)).FirstOrDefault();
    }

    public async Task<List<CoreTaskImplementation>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        return await QueryTasksAsync(query => query, cancellationToken);
    }

    public async Task<List<CoreTaskImplementation>> GetTasksAssignedToUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await QueryTasksAsync(query => query.Where(it => it.Assignee == userId), cancellationToken);
    }

    public async Task<List<CoreTaskImplementation>> GetTasksByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await QueryTasksAsync(query => query.Where(it => it.ProcessInstanceId == processInstanceId), cancellationToken);
    }

    public async Task<List<CoreTaskImplementation>> GetSubTasksAsync(string parentTaskId, CancellationToken cancellationToken = default)
    {
        return await QueryTasksAsync(query => query.Where(it => it.ParentTaskId == parentTaskId), cancellationToken);
    }

    public async Task<List<CoreIdentityLinkEntity>> GetIdentityLinksForTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        return await QueryIdentityLinksAsync(query => query.Where(it => it.TaskId == taskId), cancellationToken);
    }

    public async Task<List<CoreIdentityLinkEntity>> GetIdentityLinksForProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await QueryIdentityLinksAsync(query => query.Where(it => it.ProcessInstanceId == processInstanceId), cancellationToken);
    }

    public async Task<List<CoreIdentityLinkEntity>> GetIdentityLinksForProcessDefinitionAsync(string processDefinitionId, CancellationToken cancellationToken = default)
    {
        return await QueryIdentityLinksAsync(query => query.Where(it => it.ProcessDefinitionId == processDefinitionId), cancellationToken);
    }

    public async Task<CoreAttachmentEntity?> GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        return await LoadByIdAsync(
            _db.Queryable<PersistentAttachmentEntity>(),
            attachmentId,
            SqlSugarWorkflowPersistenceMapper.MapAttachment);
    }

    public async Task<byte[]?> GetAttachmentContentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        var contentId = await GetAttachmentContentIdAsync(attachmentId, cancellationToken);
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return null;
        }

        var content = await _db.Queryable<PersistentByteArrayEntity>().InSingleAsync(contentId);
        return content?.Bytes?.ToArray();
    }

    public async Task<List<CoreAttachmentEntity>> GetAttachmentsAsync(CancellationToken cancellationToken = default)
    {
        return await QueryAttachmentsAsync(query => query, cancellationToken);
    }

    public async Task<List<CoreAttachmentEntity>> GetTaskAttachmentsAsync(string taskId, CancellationToken cancellationToken = default)
    {
        return await QueryAttachmentsAsync(query => query.Where(it => it.TaskId == taskId), cancellationToken);
    }

    public async Task<List<CoreAttachmentEntity>> GetProcessInstanceAttachmentsAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await QueryAttachmentsAsync(query => query.Where(it => it.ProcessInstanceId == processInstanceId), cancellationToken);
    }

    public async Task PersistAttachmentAsync(CoreAttachmentEntity attachment, byte[]? content = null, CancellationToken cancellationToken = default)
    {
        var contentId = attachment.ContentId;
        if (content != null && string.IsNullOrWhiteSpace(contentId))
        {
            contentId = attachment.Id;
        }

        await UpsertByIdAsync(new PersistentAttachmentEntity
        {
            Id = attachment.Id,
            Revision = 1,
            UserId = attachment.UserId ?? string.Empty,
            Name = attachment.Name ?? string.Empty,
            Description = attachment.Description ?? string.Empty,
            Type = attachment.Type ?? string.Empty,
            TaskId = attachment.TaskId ?? string.Empty,
            ProcessInstanceId = attachment.ProcessInstanceId ?? string.Empty,
            Url = attachment.Url ?? string.Empty,
            ContentId = contentId,
            CreateTime = attachment.Time
        }, attachment.Id, cancellationToken);

        if (content != null)
        {
            var byteArrayId = contentId ?? attachment.Id;
            await UpsertByIdAsync(new PersistentByteArrayEntity
            {
                Id = byteArrayId,
                Name = attachment.Name,
                Bytes = content.ToArray(),
                Generated = false
            }, byteArrayId, cancellationToken);
        }
    }

    public async Task DeleteAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        var contentId = await GetAttachmentContentIdAsync(attachmentId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(contentId))
        {
            await _db.Deleteable<PersistentByteArrayEntity>()
                .In(contentId)
                .ExecuteCommandAsync(cancellationToken);
        }

        await _db.Deleteable<PersistentAttachmentEntity>()
            .In(attachmentId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<List<CoreCommentEntity>> GetCommentsAsync(CancellationToken cancellationToken = default)
    {
        return await QueryCommentsAsync(query => query, cancellationToken);
    }

    public async Task<CoreCommentEntity?> GetCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        return await LoadByIdAsync(
            _db.Queryable<PersistentCommentEntity>(),
            commentId,
            SqlSugarWorkflowPersistenceMapper.MapComment);
    }

    public async Task<List<CoreCommentEntity>> GetTaskCommentsAsync(string taskId, string? type = null, CancellationToken cancellationToken = default)
    {
        return await QueryCommentsAsync(query =>
        {
            query = query.Where(it => it.TaskId == taskId);
            return !string.IsNullOrWhiteSpace(type) ? query.Where(it => it.Type == type) : query;
        }, cancellationToken);
    }

    public async Task<List<CoreCommentEntity>> GetCommentsByTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        return await QueryCommentsAsync(query => query.Where(it => it.Type == type), cancellationToken);
    }

    public async Task<List<CoreCommentEntity>> GetProcessInstanceCommentsAsync(string processInstanceId, string? type = null, CancellationToken cancellationToken = default)
    {
        return await QueryCommentsAsync(query =>
        {
            query = query.Where(it => it.ProcessInstanceId == processInstanceId);
            return !string.IsNullOrWhiteSpace(type) ? query.Where(it => it.Type == type) : query;
        }, cancellationToken);
    }

    public Task PersistCommentAsync(CoreCommentEntity comment, CancellationToken cancellationToken = default)
    {
        return UpsertAsync(new PersistentCommentEntity
        {
            Id = comment.Id,
            Action = comment.Action,
            FullMessage = comment.FullMessage,
            Message = comment.Message,
            ProcessInstanceId = comment.ProcessInstanceId,
            TaskId = comment.TaskId,
            Time = comment.Time,
            Type = comment.Type,
            UserId = comment.UserId
        }, cancellationToken);
    }

    public Task DeleteCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<PersistentCommentEntity>()
            .In(commentId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<List<HistoricProcessInstance>> GetHistoricProcessInstancesAsync(CancellationToken cancellationToken = default)
    {
        return await QueryOrderedAsync(
            _db.Queryable<PersistentHistoricProcessEntity>(),
            query => query.OrderBy(it => it.StartTime, OrderByType.Asc),
            SqlSugarWorkflowPersistenceMapper.MapHistoricProcess,
            cancellationToken);
    }

    public async Task<HistoricProcessInstance?> GetHistoricProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await LoadByIdAsync(
            _db.Queryable<PersistentHistoricProcessEntity>(),
            processInstanceId,
            SqlSugarWorkflowPersistenceMapper.MapHistoricProcess);
    }

    public async Task<List<HistoricTaskInstance>> GetHistoricTaskInstancesAsync(CancellationToken cancellationToken = default)
    {
        return await QueryOrderedAsync(
            _db.Queryable<PersistentHistoricTaskEntity>(),
            query => query.OrderBy(it => it.StartTime, OrderByType.Asc),
            SqlSugarWorkflowPersistenceMapper.MapHistoricTask,
            cancellationToken);
    }

    public async Task<List<HistoricActivityInstance>> GetHistoricActivityInstancesAsync(CancellationToken cancellationToken = default)
    {
        return await QueryOrderedAsync(
            _db.Queryable<PersistentHistoricActivityEntity>(),
            query => query.OrderBy(it => it.StartTime, OrderByType.Asc),
            SqlSugarWorkflowPersistenceMapper.MapHistoricActivity,
            cancellationToken);
    }

    public async Task<List<HistoricVariableInstance>> GetHistoricVariableInstancesAsync(CancellationToken cancellationToken = default)
    {
        return await QueryByteArrayBackedAsync(
            _db.Queryable<PersistentHistoricVariableEntity>(),
            orderedQuery => orderedQuery.OrderBy(it => it.CreateTime, OrderByType.Asc),
            entity => entity.ByteArrayId,
            (entity, bytes) => SqlSugarWorkflowPersistenceMapper.MapHistoricVariable(entity, bytes),
            cancellationToken);
    }

    public async Task<List<HistoricDetail>> GetHistoricDetailsAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await QueryByteArrayBackedAsync(
            _db.Queryable<PersistentHistoricDetailEntity>()
                .Where(it => it.ProcessInstanceId == processInstanceId),
            orderedQuery => orderedQuery.OrderBy(it => it.Time, OrderByType.Asc),
            entity => entity.ByteArrayId,
            (entity, bytes) => SqlSugarWorkflowPersistenceMapper.MapHistoricDetail(entity, bytes),
            cancellationToken);
    }

    public async Task<List<HistoricIdentityLink>> GetHistoricIdentityLinksAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await QueryHistoricIdentityLinksAsync(
            query => query.Where(it => it.ProcessInstanceId == processInstanceId),
            SqlSugarWorkflowPersistenceMapper.MapHistoricIdentityLink,
            cancellationToken);
    }

    public async Task<List<CoreIdentityLinkEntity>> GetHistoricIdentityLinksForProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await QueryHistoricIdentityLinksAsync(
            query => query.Where(it => it.ProcessInstanceId == processInstanceId),
            SqlSugarWorkflowPersistenceMapper.MapHistoricIdentityLinkEntity,
            cancellationToken);
    }

    public async Task<List<CoreIdentityLinkEntity>> GetHistoricIdentityLinksForTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        return await QueryHistoricIdentityLinksAsync(
            query => query.Where(it => it.TaskId == taskId),
            SqlSugarWorkflowPersistenceMapper.MapHistoricIdentityLinkEntity,
            cancellationToken);
    }

    public async Task DeleteHistoricProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        await DeleteByWhereAsync(_db.Deleteable<PersistentHistoricProcessEntity>().Where(it => it.Id == processInstanceId), cancellationToken);
        await DeleteByWhereAsync(_db.Deleteable<PersistentHistoricTaskEntity>().Where(it => it.ProcessInstanceId == processInstanceId), cancellationToken);
        await DeleteByWhereAsync(_db.Deleteable<PersistentHistoricActivityEntity>().Where(it => it.ProcessInstanceId == processInstanceId), cancellationToken);
        await DeleteByWhereAsync(_db.Deleteable<PersistentHistoricVariableEntity>().Where(it => it.ProcessInstanceId == processInstanceId), cancellationToken);
        await DeleteByWhereAsync(_db.Deleteable<PersistentHistoricDetailEntity>().Where(it => it.ProcessInstanceId == processInstanceId), cancellationToken);
        await DeleteByWhereAsync(_db.Deleteable<PersistentHistoricIdentityLinkEntity>().Where(it => it.ProcessInstanceId == processInstanceId), cancellationToken);
    }

    public async Task DeleteHistoricTaskInstanceAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await DeleteByWhereAsync(_db.Deleteable<PersistentHistoricTaskEntity>().Where(it => it.Id == taskId), cancellationToken);
        await DeleteByWhereAsync(_db.Deleteable<PersistentHistoricIdentityLinkEntity>().Where(it => it.TaskId == taskId), cancellationToken);
    }

    private async Task LoadDefinitionsAsync(IProcessEngineConfiguration processEngineConfiguration, CancellationToken cancellationToken)
    {
        SqlSugarWorkflowDefinitionCache.EnsureProcessDefinitionCache(processEngineConfiguration);

        var definitions = await _db.Queryable<PersistentProcessDefinitionEntity>()
            .OrderBy(it => it.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        if (definitions.Count == 0)
        {
            return;
        }

        var parser = new BpmnXmlParser();
        var repositoryService = new RepositoryServiceImplementation(processEngineConfiguration);
        var behaviorFactory = new DefaultActivityBehaviorFactory(
            _serviceProvider,
            processEngineConfiguration.ExpressionManager,
            eventDispatcher: processEngineConfiguration.EventDispatcher,
            jobManager: processEngineConfiguration.JobManager,
            processDefinitionManager: new CallActivityProcessDefinitionManager(repositoryService),
            runtimeService: new RuntimeServiceImplementation(processEngineConfiguration));
        var definitionsWithResource = definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.ResourceName) && !string.IsNullOrWhiteSpace(definition.DeploymentId))
            .ToList();
        var deploymentIds = definitionsWithResource
            .Select(definition => definition.DeploymentId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var resourceNames = definitionsWithResource
            .Select(definition => definition.ResourceName!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var resources = deploymentIds.Count == 0 || resourceNames.Count == 0
            ? new List<PersistentResourceEntity>()
            : await _db.Queryable<PersistentResourceEntity>()
                .Where(it => deploymentIds.Contains(it.DeploymentId) && resourceNames.Contains(it.Name))
                .ToListAsync(cancellationToken);
        var resourceMap = resources.ToDictionary(
            resource => $"{resource.DeploymentId}:{resource.Name}",
            resource => resource,
            StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.ResourceName) || string.IsNullOrWhiteSpace(definition.DeploymentId))
            {
                continue;
            }

            if (!resourceMap.TryGetValue($"{definition.DeploymentId}:{definition.ResourceName}", out var resource))
            {
                continue;
            }

            if (resource?.Bytes == null)
            {
                continue;
            }

            var bpmnXml = Encoding.UTF8.GetString(resource.Bytes);
            var model = parser.Parse(bpmnXml);
            foreach (var process in model.Processes)
            {
                BpmnBehaviorBinder.BindBehaviors(process, behaviorFactory);
            }

            var processModel = model.GetProcessById(definition.Key) ?? model.Processes.FirstOrDefault();
            if (processModel == null)
            {
                continue;
            }

            SqlSugarWorkflowDefinitionCache.RegisterProcessDefinition(processEngineConfiguration, definition, model, processModel);
        }
    }

    private static IEnumerable<CoreExecutionEntity> FlattenExecutions(IEnumerable<CoreExecutionEntity> roots)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<CoreExecutionEntity>(roots);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current.Id))
            {
                continue;
            }

            yield return current;
            foreach (var child in current.ChildExecutions)
            {
                stack.Push(child);
            }
        }
    }

    private static PersistentExecutionEntity MapRuntimeExecution(CoreExecutionEntity execution)
    {
        return new PersistentExecutionEntity
        {
            Id = execution.Id,
            Revision = execution.Revision,
            ProcessInstanceId = execution.ProcessInstanceId,
            ProcessDefinitionId = execution.ProcessDefinitionId,
            ParentId = execution.ParentId,
            SuperExecutionId = execution.SuperExecutionId,
            ActivityId = execution.CurrentFlowElementId ?? execution.ActivityId,
            IsActive = execution.IsActive,
            IsConcurrent = execution.IsConcurrent,
            IsScope = execution.IsScope,
            IsEnded = execution.IsEnded,
            SuspensionState = execution.IsEnded ? 1 : (execution.IsActive ? 1 : 2),
            TenantId = execution.TenantId,
            BusinessKey = execution.BusinessKey,
            Name = execution.CurrentActivityName
        };
    }

    private static IEnumerable<PersistentTaskEntity> MapRuntimeTasks(CoreExecutionEntity execution)
    {
        foreach (var task in execution.TaskEntities)
        {
            yield return new PersistentTaskEntity
            {
                Id = task.Id,
                ExecutionId = execution.Id,
                ProcessInstanceId = task.ProcessInstanceId ?? execution.ProcessInstanceId,
                ProcessDefinitionId = task.ProcessDefinitionId ?? execution.ProcessDefinitionId,
                Name = task.Name,
                ParentTaskId = task.ParentTaskId,
                Description = task.Description,
                TaskDefinitionKey = task.TaskDefinitionKey,
                Owner = task.Owner,
                Assignee = task.Assignee,
                DelegationState = task.DelegationState,
                Priority = task.Priority,
                CreateTime = task.CreateTime,
                DueDate = task.DueDate,
                Category = task.Category,
                FormKey = task.FormKey,
                SuspensionState = execution.IsActive ? 1 : 2
            };
        }
    }

    private static PersistentTaskEntity MapStandaloneTask(CoreTaskImplementation task)
    {
        return new PersistentTaskEntity
        {
            Id = task.Id,
            ProcessInstanceId = task.ProcessInstanceId,
            ProcessDefinitionId = task.ProcessDefinitionId,
            Name = task.Name,
            ParentTaskId = task.ParentTaskId,
            Description = task.Description,
            TaskDefinitionKey = task.TaskDefinitionKey,
            Owner = task.Owner,
            Assignee = task.Assignee,
            DelegationState = task.DelegationState,
            Priority = task.Priority,
            CreateTime = task.CreateTime,
            DueDate = task.DueDate,
            Category = task.Category,
            FormKey = task.FormKey,
            SuspensionState = 1
        };
    }

    private static IEnumerable<RuntimeVariableMapping> MapRuntimeVariables(CoreExecutionEntity execution)
    {
        foreach (var variable in execution.Variables)
        {
            yield return PersistenceVariableCodec.EncodeRuntimeVariable(
                execution.Id,
                execution.ProcessInstanceId,
                execution.CurrentFlowElementId ?? execution.ActivityId,
                variable);
        }
    }

    private static IEnumerable<PersistentIdentityLinkEntity> MapRuntimeIdentityLinks(CoreExecutionEntity execution)
    {
        foreach (var task in execution.TaskEntities)
        {
            if (!string.IsNullOrWhiteSpace(task.Assignee))
            {
                yield return new PersistentIdentityLinkEntity
                {
                    Id = $"{task.Id}:assignee:{task.Assignee}",
                    TaskId = task.Id,
                    ProcessInstanceId = task.ProcessInstanceId,
                    ProcessDefinitionId = task.ProcessDefinitionId,
                    UserId = task.Assignee,
                    Type = AssigneeIdentityLinkType
                };
            }

            if (!string.IsNullOrWhiteSpace(task.Owner))
            {
                yield return new PersistentIdentityLinkEntity
                {
                    Id = $"{task.Id}:owner:{task.Owner}",
                    TaskId = task.Id,
                    ProcessInstanceId = task.ProcessInstanceId,
                    ProcessDefinitionId = task.ProcessDefinitionId,
                    UserId = task.Owner,
                    Type = OwnerIdentityLinkType
                };
            }

            foreach (var candidateUser in task.CandidateUsers ?? Enumerable.Empty<string>())
            {
                yield return new PersistentIdentityLinkEntity
                {
                    Id = $"{task.Id}:candidate-user:{candidateUser}",
                    TaskId = task.Id,
                    ProcessInstanceId = task.ProcessInstanceId,
                    ProcessDefinitionId = task.ProcessDefinitionId,
                    UserId = candidateUser,
                    Type = CandidateIdentityLinkType
                };
            }

            foreach (var candidateGroup in task.CandidateGroups ?? Enumerable.Empty<string>())
            {
                yield return new PersistentIdentityLinkEntity
                {
                    Id = $"{task.Id}:candidate-group:{candidateGroup}",
                    TaskId = task.Id,
                    ProcessInstanceId = task.ProcessInstanceId,
                    ProcessDefinitionId = task.ProcessDefinitionId,
                    GroupId = candidateGroup,
                    Type = CandidateIdentityLinkType
                };
            }
        }

        if (execution.IsProcessInstanceType &&
            execution.Variables.TryGetValue("initiator", out var initiatorValue) &&
            initiatorValue is string initiator &&
            !string.IsNullOrWhiteSpace(initiator))
        {
            yield return new PersistentIdentityLinkEntity
            {
                Id = $"{execution.ProcessInstanceId}:starter:{initiator}",
                ProcessInstanceId = execution.ProcessInstanceId,
                ProcessDefinitionId = execution.ProcessDefinitionId,
                UserId = initiator,
                Type = StarterIdentityLinkType
            };
        }
    }

    private static IEnumerable<EventSubscriptionEntity> MapRuntimeEventSubscriptions(CoreExecutionEntity execution)
    {
        var currentFlowElement = execution.CurrentFlowElement;
        if (currentFlowElement is CatchEvent catchEvent)
        {
            foreach (var eventDefinition in catchEvent.EventDefinitions)
            {
                switch (eventDefinition)
                {
                    case MessageEventDefinition messageEventDefinition:
                    {
                        var messageName = ResolveMessageName(execution, messageEventDefinition);
                        if (!string.IsNullOrWhiteSpace(messageName))
                        {
                            yield return new EventSubscriptionEntity
                            {
                                Id = $"{execution.Id}:message:{messageName}",
                                EventType = MessageEventSubscriptionEntity.EventTypeMessage,
                                EventName = messageName,
                                ExecutionId = execution.Id,
                                ProcessInstanceId = execution.ProcessInstanceId,
                                ActivityId = execution.CurrentFlowElementId ?? execution.ActivityId,
                                ProcessDefinitionId = execution.ProcessDefinitionId,
                                TenantId = execution.TenantId
                            };
                        }

                        break;
                    }

                    case SignalEventDefinition signalEventDefinition:
                    {
                        var signalName = ResolveSignalName(execution, signalEventDefinition);
                        if (!string.IsNullOrWhiteSpace(signalName))
                        {
                            yield return new EventSubscriptionEntity
                            {
                                Id = $"{execution.Id}:signal:{signalName}",
                                EventType = SignalEventSubscriptionEntity.EventTypeSignal,
                                EventName = signalName,
                                ExecutionId = execution.Id,
                                ProcessInstanceId = execution.ProcessInstanceId,
                                ActivityId = execution.CurrentFlowElementId ?? execution.ActivityId,
                                ProcessDefinitionId = execution.ProcessDefinitionId,
                                TenantId = execution.TenantId
                            };
                        }

                        break;
                    }
                }
            }
        }

        // Backward-compat fallback: keep honoring existing runtime marker variables.
        // New subscriptions should come from BPMN catch-event definitions above.
        if (execution.Variables.TryGetValue("_messageSubscriptionName", out var messageNameObj) &&
            messageNameObj is string fallbackMessageName &&
            !string.IsNullOrWhiteSpace(fallbackMessageName))
        {
            yield return new EventSubscriptionEntity
            {
                Id = $"{execution.Id}:message:{fallbackMessageName}",
                EventType = MessageEventSubscriptionEntity.EventTypeMessage,
                EventName = fallbackMessageName,
                ExecutionId = execution.Id,
                ProcessInstanceId = execution.ProcessInstanceId,
                ActivityId = execution.CurrentFlowElementId ?? execution.ActivityId,
                ProcessDefinitionId = execution.ProcessDefinitionId,
                TenantId = execution.TenantId
            };
        }

        if (execution.Variables.TryGetValue("_signalSubscriptionName", out var signalNameObj) &&
            signalNameObj is string fallbackSignalName &&
            !string.IsNullOrWhiteSpace(fallbackSignalName))
        {
            yield return new EventSubscriptionEntity
            {
                Id = $"{execution.Id}:signal:{fallbackSignalName}",
                EventType = SignalEventSubscriptionEntity.EventTypeSignal,
                EventName = fallbackSignalName,
                ExecutionId = execution.Id,
                ProcessInstanceId = execution.ProcessInstanceId,
                ActivityId = execution.CurrentFlowElementId ?? execution.ActivityId,
                ProcessDefinitionId = execution.ProcessDefinitionId,
                TenantId = execution.TenantId
            };
        }
    }

    private static string? ResolveMessageName(CoreExecutionEntity execution, MessageEventDefinition eventDefinition)
    {
        if (string.IsNullOrWhiteSpace(eventDefinition.MessageRef))
        {
            return null;
        }

        var messageRef = eventDefinition.MessageRef!;
        var bpmnModel = execution.Process?.BpmnModel;
        if (bpmnModel != null && bpmnModel.MessageMap.TryGetValue(messageRef, out var message))
        {
            return !string.IsNullOrWhiteSpace(message.Name) ? message.Name : messageRef;
        }

        return messageRef;
    }

    private static string? ResolveSignalName(CoreExecutionEntity execution, SignalEventDefinition eventDefinition)
    {
        if (string.IsNullOrWhiteSpace(eventDefinition.SignalRef))
        {
            return null;
        }

        var signalRef = eventDefinition.SignalRef!;
        var bpmnModel = execution.Process?.BpmnModel;
        if (bpmnModel != null)
        {
            var signal = bpmnModel.Signals.FirstOrDefault(item =>
                string.Equals(item.Id, signalRef, StringComparison.Ordinal));
            if (signal != null)
            {
                return !string.IsNullOrWhiteSpace(signal.Name) ? signal.Name : signalRef;
            }
        }

        var processSignal = execution.Process?.Signals.FirstOrDefault(item =>
            string.Equals(item.Id, signalRef, StringComparison.Ordinal));
        if (processSignal != null)
        {
            return !string.IsNullOrWhiteSpace(processSignal.Name) ? processSignal.Name : signalRef;
        }

        return signalRef;
    }

    private async Task<Dictionary<string, byte[]>> LoadByteArraysAsync(
        IEnumerable<PersistentVariableEntity> variableRows,
        CancellationToken cancellationToken)
    {
        return await LoadByteArraysByIdsAsync(variableRows
            .Where(row => !string.IsNullOrWhiteSpace(row.ByteArrayId))
            .Select(row => row.ByteArrayId!)
            .Distinct(StringComparer.Ordinal), cancellationToken);
    }

    private async Task<Dictionary<string, byte[]>> LoadByteArraysByIdsAsync(
        IEnumerable<string?> byteArrayIds,
        CancellationToken cancellationToken)
    {
        var ids = byteArrayIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }

        var byteArrays = await _db.Queryable<PersistentByteArrayEntity>()
            .In(ids)
            .ToListAsync(cancellationToken);

        return byteArrays
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && item.Bytes != null)
            .ToDictionary(item => item.Id, item => item.Bytes, StringComparer.Ordinal);
    }

    private async Task<List<CoreTaskImplementation>> MapTasksAsync(List<PersistentTaskEntity> tasks, CancellationToken cancellationToken)
    {
        if (tasks.Count == 0)
        {
            return new List<CoreTaskImplementation>();
        }

        var taskIds = tasks
            .Select(task => task.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var links = taskIds.Count == 0
            ? new List<PersistentIdentityLinkEntity>()
            : await _db.Queryable<PersistentIdentityLinkEntity>()
                .Where(it => !string.IsNullOrWhiteSpace(it.TaskId) && taskIds.Contains(it.TaskId!))
                .ToListAsync(cancellationToken);
        var linksByTaskId = GroupIdentityLinksByTaskId(links);

        return tasks
            .Select(task => SqlSugarWorkflowPersistenceMapper.MapTask(task, linksByTaskId.GetValueOrDefault(task.Id, Array.Empty<PersistentIdentityLinkEntity>())))
            .ToList();
    }

    private async Task<List<CoreTaskImplementation>> QueryTasksAsync(
        Func<ISugarQueryable<PersistentTaskEntity>, ISugarQueryable<PersistentTaskEntity>> buildQuery,
        CancellationToken cancellationToken)
    {
        var tasks = await buildQuery(_db.Queryable<PersistentTaskEntity>())
            .OrderBy(it => it.CreateTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return await MapTasksAsync(tasks, cancellationToken);
    }

    private Task<PersistentAttachmentEntity?> GetAttachmentRowByIdAsync(string attachmentId)
    {
        return _db.Queryable<PersistentAttachmentEntity>().InSingleAsync(attachmentId);
    }

    private static CoreIntegrationContextEntity MapIntegrationContext(PersistentIntegrationContextEntity entity)
    {
        return new CoreIntegrationContextEntity
        {
            Id = entity.Id,
            ExecutionId = entity.ExecutionId,
            ProcessInstanceId = entity.ProcessInstanceId,
            ProcessDefinitionId = entity.ProcessDefinitionId,
            FlowNodeId = entity.FlowNodeId,
            ConnectorId = entity.ConnectorId,
            CorrelationId = entity.CorrelationId,
            Status = entity.Status,
            ResultType = entity.ResultType
        };
    }

    private static Task DeleteByWhereAsync<T>(
        IDeleteable<T> deleteable,
        CancellationToken cancellationToken)
        where T : class, new()
    {
        return deleteable.ExecuteCommandAsync(cancellationToken);
    }

    private static async Task<TOutput?> LoadByIdAsync<TEntity, TOutput>(
        ISugarQueryable<TEntity> query,
        string id,
        Func<TEntity, TOutput> map)
        where TEntity : class, new()
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return default;
        }

        var entity = await query.InSingleAsync(id);
        return entity == null ? default : map(entity);
    }

    private static async Task<List<TOutput>> QueryOrderedAsync<TEntity, TOutput>(
        ISugarQueryable<TEntity> query,
        Func<ISugarQueryable<TEntity>, ISugarQueryable<TEntity>> orderQuery,
        Func<TEntity, TOutput> map,
        CancellationToken cancellationToken)
        where TEntity : class, new()
    {
        var entities = await orderQuery(query).ToListAsync(cancellationToken);
        return entities.Select(map).ToList();
    }

    private async Task<List<TOutput>> QueryByteArrayBackedAsync<TEntity, TOutput>(
        ISugarQueryable<TEntity> query,
        Func<ISugarQueryable<TEntity>, ISugarQueryable<TEntity>> orderQuery,
        Func<TEntity, string?> byteArrayIdSelector,
        Func<TEntity, byte[]?, TOutput> map,
        CancellationToken cancellationToken)
        where TEntity : class, new()
    {
        var entities = await orderQuery(query).ToListAsync(cancellationToken);
        var byteArrayMap = await LoadByteArraysByIdsAsync(entities.Select(byteArrayIdSelector), cancellationToken);
        return entities.Select(entity => map(entity, ResolveByteArrayContent(byteArrayMap, byteArrayIdSelector(entity)))).ToList();
    }

    private async Task<List<CoreIdentityLinkEntity>> QueryIdentityLinksAsync(
        Func<ISugarQueryable<PersistentIdentityLinkEntity>, ISugarQueryable<PersistentIdentityLinkEntity>> buildQuery,
        CancellationToken cancellationToken)
    {
        var entities = await buildQuery(_db.Queryable<PersistentIdentityLinkEntity>())
            .ToListAsync(cancellationToken);
        return entities.Select(SqlSugarWorkflowPersistenceMapper.MapIdentityLink).ToList();
    }

    private async Task<List<CoreAttachmentEntity>> QueryAttachmentsAsync(
        Func<ISugarQueryable<PersistentAttachmentEntity>, ISugarQueryable<PersistentAttachmentEntity>> buildQuery,
        CancellationToken cancellationToken)
    {
        var entities = await buildQuery(_db.Queryable<PersistentAttachmentEntity>())
            .OrderBy(it => it.CreateTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return entities.Select(SqlSugarWorkflowPersistenceMapper.MapAttachment).ToList();
    }

    private async Task<List<CoreCommentEntity>> QueryCommentsAsync(
        Func<ISugarQueryable<PersistentCommentEntity>, ISugarQueryable<PersistentCommentEntity>> buildQuery,
        CancellationToken cancellationToken)
    {
        var entities = await buildQuery(_db.Queryable<PersistentCommentEntity>())
            .OrderBy(it => it.Time, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return entities.Select(SqlSugarWorkflowPersistenceMapper.MapComment).ToList();
    }

    private async Task<List<TIdentityLink>> QueryHistoricIdentityLinksAsync<TIdentityLink>(
        Func<ISugarQueryable<PersistentHistoricIdentityLinkEntity>, ISugarQueryable<PersistentHistoricIdentityLinkEntity>> buildQuery,
        Func<PersistentHistoricIdentityLinkEntity, TIdentityLink> map,
        CancellationToken cancellationToken)
    {
        var entities = await buildQuery(_db.Queryable<PersistentHistoricIdentityLinkEntity>())
            .ToListAsync(cancellationToken);
        return entities.Select(map).ToList();
    }

    private async Task<string?> GetAttachmentContentIdAsync(string attachmentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(attachmentId))
        {
            return null;
        }

        return await _db.Queryable<PersistentAttachmentEntity>()
            .Where(it => it.Id == attachmentId)
            .Select(it => it.ContentId)
            .FirstAsync(cancellationToken);
    }

    private static byte[]? ResolveByteArrayContent(
        IReadOnlyDictionary<string, byte[]> byteArrayMap,
        string? byteArrayId)
    {
        if (string.IsNullOrWhiteSpace(byteArrayId))
        {
            return null;
        }

        return byteArrayMap.TryGetValue(byteArrayId, out var bytes) ? bytes : null;
    }

    private static Dictionary<string, IReadOnlyCollection<PersistentIdentityLinkEntity>> GroupIdentityLinksByTaskId(
        IEnumerable<PersistentIdentityLinkEntity> identityLinks)
    {
        return identityLinks
            .Where(link => !string.IsNullOrWhiteSpace(link.TaskId))
            .GroupBy(link => link.TaskId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<PersistentIdentityLinkEntity>)group.ToList(),
                StringComparer.Ordinal);
    }

    private async Task PersistExecutionsWithOptimisticLockAsync(
        IReadOnlyCollection<PersistentExecutionEntity> entities,
        CancellationToken cancellationToken)
    {
        foreach (var entity in entities)
        {
            var existingRevision = await _db.Queryable<PersistentExecutionEntity>()
                .Where(it => it.Id == entity.Id)
                .Select(it => it.Revision)
                .FirstAsync(cancellationToken);

            var loadedRevision = entity.Revision;
            if (loadedRevision > 0 && loadedRevision != existingRevision)
            {
                throw new WorkflowEngineOptimisticLockingException(
                    $"Optimistic locking failed for execution '{entity.Id}': expected revision {loadedRevision}, but database has revision {existingRevision}.");
            }

            entity.Revision = existingRevision <= 0 ? 1 : existingRevision + 1;
            await UpsertByIdAsync(entity, entity.Id, cancellationToken);
        }
    }

    private async Task PrepareTaskRevisionsAsync(
        IReadOnlyCollection<PersistentTaskEntity> entities,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        var ids = entities.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToList();
        var existingRows = await _db.Queryable<PersistentTaskEntity>()
            .In(ids)
            .Select(item => new { item.Id, item.Revision })
            .ToListAsync(cancellationToken);
        var existingRevisionById = existingRows.ToDictionary(item => item.Id, item => item.Revision, StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            if (existingRevisionById.TryGetValue(entity.Id, out var existingRevision))
            {
                if (entity.Revision > 0 && entity.Revision != existingRevision)
                {
                    throw new WorkflowEngineOptimisticLockingException(
                        $"Optimistic locking failed for task '{entity.Id}': expected revision {existingRevision}, got {entity.Revision}.");
                }

                entity.Revision = existingRevision + 1;
            }
            else if (entity.Revision <= 0)
            {
                entity.Revision = 1;
            }
        }
    }

    private async Task PrepareVariableRevisionsAsync(
        IReadOnlyCollection<PersistentVariableEntity> entities,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        var ids = entities.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToList();
        var existingRows = await _db.Queryable<PersistentVariableEntity>()
            .In(ids)
            .Select(item => new { item.Id, item.Revision })
            .ToListAsync(cancellationToken);
        var existingRevisionById = existingRows.ToDictionary(item => item.Id, item => item.Revision, StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            if (existingRevisionById.TryGetValue(entity.Id, out var existingRevision))
            {
                if (entity.Revision > 0 && entity.Revision != existingRevision)
                {
                    throw new WorkflowEngineOptimisticLockingException(
                        $"Optimistic locking failed for variable '{entity.Id}': expected revision {existingRevision}, got {entity.Revision}.");
                }

                entity.Revision = existingRevision + 1;
            }
            else if (entity.Revision <= 0)
            {
                entity.Revision = 1;
            }
        }
    }

    private async Task PrepareIdentityLinkRevisionsAsync(
        IReadOnlyCollection<PersistentIdentityLinkEntity> entities,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        var ids = entities.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToList();
        var existingRows = await _db.Queryable<PersistentIdentityLinkEntity>()
            .In(ids)
            .Select(item => new { item.Id, item.Revision })
            .ToListAsync(cancellationToken);
        var existingRevisionById = existingRows.ToDictionary(item => item.Id, item => item.Revision, StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            if (existingRevisionById.TryGetValue(entity.Id, out var existingRevision))
            {
                if (entity.Revision > 0 && entity.Revision != existingRevision)
                {
                    throw new WorkflowEngineOptimisticLockingException(
                        $"Optimistic locking failed for identity link '{entity.Id}': expected revision {existingRevision}, got {entity.Revision}.");
                }

                entity.Revision = existingRevision + 1;
            }
            else if (entity.Revision <= 0)
            {
                entity.Revision = 1;
            }
        }
    }

    private async Task PrepareEventSubscriptionRevisionsAsync(
        IReadOnlyCollection<EventSubscriptionEntity> entities,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        var ids = entities.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToList();
        var existingRows = await _db.Queryable<EventSubscriptionEntity>()
            .In(ids)
            .Select(item => new { item.Id, item.Revision })
            .ToListAsync(cancellationToken);
        var existingRevisionById = existingRows.ToDictionary(item => item.Id, item => item.Revision, StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            if (existingRevisionById.TryGetValue(entity.Id, out var existingRevision))
            {
                if (entity.Revision > 0 && entity.Revision != existingRevision)
                {
                    throw new WorkflowEngineOptimisticLockingException(
                        $"Optimistic locking failed for event subscription '{entity.Id}': expected revision {existingRevision}, got {entity.Revision}.");
                }

                entity.Revision = existingRevision + 1;
            }
            else if (entity.Revision <= 0)
            {
                entity.Revision = 1;
            }
        }
    }

    private Task UpsertAsync<T>(T entity, CancellationToken cancellationToken) where T : class, new()
    {
        return _db.Storageable(entity)
            .ExecuteCommandAsync(cancellationToken);
    }

    private Task UpsertBatchAsync<T>(IReadOnlyCollection<T> entities, CancellationToken cancellationToken)
        where T : class, new()
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Storageable(entities.ToList())
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task UpsertByIdAsync<T>(T entity, string id, CancellationToken cancellationToken)
        where T : class, new()
    {
        var affectedRows = await _db.Updateable(entity)
            .WhereColumns(nameof(IEntity.Id))
            .ExecuteCommandAsync(cancellationToken);
        if (affectedRows == 0)
        {
            await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
    }

}




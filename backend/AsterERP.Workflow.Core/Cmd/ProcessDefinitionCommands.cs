using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Deploy;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public abstract class NeedsActiveProcessDefinitionCmd<T> : ICommand<T>
{
    protected readonly string _processDefinitionId;

    protected NeedsActiveProcessDefinitionCmd(string processDefinitionId)
    {
        _processDefinitionId = processDefinitionId;
    }

    public T Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_processDefinitionId))
            throw new WorkflowEngineArgumentException("Process definition id is null");

        var processDefinition = ResolveProcessDefinition(context);
        if (processDefinition == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No process definition found for id = '{_processDefinitionId}'",
                typeof(ProcessDefinitionRecord));

        if (processDefinition.IsSuspended)
            throw new WorkflowEngineException(
                $"Cannot execute operation because process definition '{processDefinition.Name}' " +
                $"(id={processDefinition.Id}) is suspended");

        return Execute(context, processDefinition);
    }

    public async Task<T> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Execute(context);
    }

    protected virtual ProcessDefinitionRecord? ResolveProcessDefinition(ICommandContext context)
    {
        var defs = context.ProcessEngineConfiguration.CommandExecutor.Execute(
            new GetProcessDefinitionsCmd());
        return defs.FirstOrDefault(d => d.Id == _processDefinitionId);
    }

    protected abstract T Execute(ICommandContext context, ProcessDefinitionRecord processDefinition);
}

public class ActivateProcessDefinitionCmd : AbstractSetProcessDefinitionStateCmd
{
    public ActivateProcessDefinitionCmd(
        string? processDefinitionId,
        string? processDefinitionKey,
        bool includeProcessInstances,
        DateTime? executionDate,
        string? tenantId)
        : base(processDefinitionId, processDefinitionKey, includeProcessInstances, executionDate, tenantId)
    {
    }

    protected override SuspensionState GetProcessDefinitionSuspensionState() => SuspensionState.Active;

    protected override string GetDelayedExecutionJobHandlerType() => TimerActivateProcessDefinitionHandler.HandlerType;

    protected override AbstractSetProcessInstanceStateCmd? GetProcessInstanceChangeStateCmd(string processInstanceId)
    {
        return new ActivateProcessInstanceCmdInternal(processInstanceId);
    }
}

internal class ActivateProcessInstanceCmdInternal : AbstractSetProcessInstanceStateCmd
{
    public ActivateProcessInstanceCmdInternal(string processInstanceId) : base(processInstanceId)
    {
    }

    protected override SuspensionState GetNewState() => SuspensionState.Active;
}

public class SuspendProcessDefinitionCmd : AbstractSetProcessDefinitionStateCmd
{
    public SuspendProcessDefinitionCmd(
        string? processDefinitionId,
        string? processDefinitionKey,
        bool includeProcessInstances,
        DateTime? executionDate,
        string? tenantId)
        : base(processDefinitionId, processDefinitionKey, includeProcessInstances, executionDate, tenantId)
    {
    }

    protected override SuspensionState GetProcessDefinitionSuspensionState() => SuspensionState.Suspended;

    protected override string GetDelayedExecutionJobHandlerType() => TimerSuspendProcessDefinitionHandler.HandlerType;

    protected override AbstractSetProcessInstanceStateCmd? GetProcessInstanceChangeStateCmd(string processInstanceId)
    {
        return new SuspendProcessInstanceCmdInternal(processInstanceId);
    }
}

internal class SuspendProcessInstanceCmdInternal : AbstractSetProcessInstanceStateCmd
{
    public SuspendProcessInstanceCmdInternal(string processInstanceId) : base(processInstanceId)
    {
    }

    protected override SuspensionState GetNewState() => SuspensionState.Suspended;
}

public class IsProcessDefinitionSuspendedCmd : ICommand<bool>
{
    private readonly string _processDefinitionId;

    public IsProcessDefinitionSuspendedCmd(string processDefinitionId)
    {
        _processDefinitionId = processDefinitionId;
    }

    public bool Execute(ICommandContext context)
    {
        var defs = context.ProcessEngineConfiguration.CommandExecutor.Execute(
            new GetProcessDefinitionsCmd());
        var def = defs.FirstOrDefault(d => d.Id == _processDefinitionId);
        return def?.IsSuspended ?? false;
    }

    public Task<bool> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

public abstract class AbstractSetProcessDefinitionStateCmd : ICommand<object?>
{
    protected readonly string? _processDefinitionId;
    protected readonly string? _processDefinitionKey;
    protected readonly bool _includeProcessInstances;
    protected readonly DateTime? _executionDate;
    protected readonly string? _tenantId;

    protected AbstractSetProcessDefinitionStateCmd(
        string? processDefinitionId,
        string? processDefinitionKey,
        bool includeProcessInstances,
        DateTime? executionDate,
        string? tenantId)
    {
        _processDefinitionId = processDefinitionId;
        _processDefinitionKey = processDefinitionKey;
        _includeProcessInstances = includeProcessInstances;
        _executionDate = executionDate;
        _tenantId = tenantId;
    }

    public object? Execute(ICommandContext context) =>
        throw new NotSupportedException("Process definition state commands are async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var processDefinitions = await FindProcessDefinitionAsync(context, cancellationToken);
        await ExecuteInternalAsync(context, processDefinitions, cancellationToken);
        return null;
    }

    protected virtual async Task ExecuteInternalAsync(
        ICommandContext context,
        List<ProcessDefinitionRecord> processDefinitions,
        CancellationToken cancellationToken)
    {
        if (_executionDate == null)
        {
            await ChangeProcessDefinitionStateAsync(context, processDefinitions, cancellationToken);
        }
        else
        {
            await CreateTimerForDelayedExecutionAsync(context, processDefinitions, cancellationToken);
        }
    }

    protected virtual async Task CreateTimerForDelayedExecutionAsync(
        ICommandContext context,
        List<ProcessDefinitionRecord> processDefinitions,
        CancellationToken cancellationToken)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager == null)
            throw new WorkflowEngineArgumentException("Cannot schedule delayed process definition state change when job manager is null");

        foreach (var processDefinition in processDefinitions)
        {
            var timerJob = await jobManager.CreateTimerJobAsync(
                    string.Empty,
                    string.Empty,
                    processDefinition.Id,
                    _executionDate,
                    null,
                    GetDelayedExecutionJobHandlerType(),
                    TimerChangeProcessDefinitionSuspensionStateJobHandler.CreateJobHandlerConfiguration(_includeProcessInstances),
                    processDefinition.TenantId,
                    cancellationToken);

            await jobManager.ScheduleTimerJobAsync(timerJob, cancellationToken);
        }
    }

    protected virtual async Task<List<ProcessDefinitionRecord>> FindProcessDefinitionAsync(
        ICommandContext context,
        CancellationToken cancellationToken)
    {
        if (_processDefinitionId == null && _processDefinitionKey == null)
            throw new WorkflowEngineArgumentException("Process definition id or key cannot be null");

        var result = new List<ProcessDefinitionRecord>();
        var defs = await context.ProcessEngineConfiguration.CommandExecutor.ExecuteAsync(
            new GetProcessDefinitionsCmd(), cancellationToken);

        if (_processDefinitionId != null)
        {
            var def = defs.FirstOrDefault(d => d.Id == _processDefinitionId);
            if (def == null)
                throw new WorkflowEngineObjectNotFoundException(
                    $"Cannot find process definition for id '{_processDefinitionId}'",
                    typeof(ProcessDefinitionRecord));
            result.Add(def);
        }
        else
        {
            foreach (var def in defs)
            {
                if (def.Key == _processDefinitionKey)
                {
                    if (_tenantId == null || _tenantId == "no-tenant")
                    {
                        if (string.IsNullOrEmpty(def.TenantId))
                            result.Add(def);
                    }
                    else
                    {
                        if (def.TenantId == _tenantId)
                            result.Add(def);
                    }
                }
            }

            if (result.Count == 0)
                throw new WorkflowEngineException($"Cannot find process definition for key '{_processDefinitionKey}'");
        }

        return result;
    }

    protected virtual async Task ChangeProcessDefinitionStateAsync(
        ICommandContext context,
        List<ProcessDefinitionRecord> processDefinitions,
        CancellationToken cancellationToken)
    {
        var suspensionState = GetProcessDefinitionSuspensionState();
        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        var deploymentManager = context.ProcessEngineConfiguration.DeploymentManager;

        foreach (var processDefinition in processDefinitions)
        {
            processDefinition.IsSuspended = suspensionState == SuspensionState.Suspended;

            var cacheEntry = deploymentManager?.ResolveProcessDefinition(processDefinition.Id);
            if (cacheEntry != null)
            {
                cacheEntry.ProcessDefinition.IsSuspended = processDefinition.IsSuspended;
                deploymentManager?.ProcessDefinitionCache?.Add(processDefinition.Id, cacheEntry);
            }

            if (eventDispatcher.IsEnabled)
            {
                eventDispatcher.DispatchEvent(
                    WorkflowEventBuilder.CreateEntityEvent(
                        WorkflowEventType.ENTITY_UPDATED,
                        processDefinition));
            }

            if (_includeProcessInstances)
            {
                var processInstanceCmd = GetProcessInstanceChangeStateCmd(processDefinition.Id);
                if (processInstanceCmd != null)
                    await processInstanceCmd.ExecuteAsync(context, cancellationToken);
            }
        }
    }

    protected abstract SuspensionState GetProcessDefinitionSuspensionState();
    protected abstract string GetDelayedExecutionJobHandlerType();
    protected abstract AbstractSetProcessInstanceStateCmd? GetProcessInstanceChangeStateCmd(string processInstanceId);
}

public class SetProcessDefinitionCategoryCmd : ICommand<object?>
{
    private readonly string _processDefinitionId;
    private readonly string? _category;

    public SetProcessDefinitionCategoryCmd(string processDefinitionId, string? category)
    {
        _processDefinitionId = processDefinitionId;
        _category = category;
    }

    public object? Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_processDefinitionId))
            throw new WorkflowEngineArgumentException("Process definition id is null");

        var deploymentManager = context.ProcessEngineConfiguration.DeploymentManager;
        if (deploymentManager == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No process definition found for id = '{_processDefinitionId}'",
                typeof(ProcessDefinitionRecord));

        var cacheEntry = deploymentManager.ResolveProcessDefinition(_processDefinitionId);
        if (cacheEntry == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No process definition found for id = '{_processDefinitionId}'",
                typeof(ProcessDefinitionRecord));

        cacheEntry.ProcessDefinition.Category = _category;
        deploymentManager.ProcessDefinitionCache?.Add(_processDefinitionId, cacheEntry);

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.ENTITY_UPDATED,
                    cacheEntry.ProcessDefinition));
        }

        return null;
    }

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

public class GetDeploymentProcessDefinitionCmd : ICommand<ProcessDefinitionRecord?>
{
    private readonly string _processDefinitionId;

    public GetDeploymentProcessDefinitionCmd(string processDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(processDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");

        _processDefinitionId = processDefinitionId;
    }

    public ProcessDefinitionRecord? Execute(ICommandContext context)
    {
        var defs = context.ProcessEngineConfiguration.CommandExecutor.Execute(
            new GetProcessDefinitionsCmd());
        var def = defs.FirstOrDefault(d => d.Id == _processDefinitionId);
        if (def == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No process definition found for id = '{_processDefinitionId}'",
                typeof(ProcessDefinitionRecord));

        return def;
    }

    public Task<ProcessDefinitionRecord?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

public class GetDeploymentResourceCmd : ICommand<byte[]?>
{
    private readonly string _deploymentId;
    private readonly string _resourceName;

    public GetDeploymentResourceCmd(string deploymentId, string resourceName)
    {
        if (string.IsNullOrWhiteSpace(deploymentId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("deploymentId is null");
        if (string.IsNullOrWhiteSpace(resourceName))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("resourceName is null");

        _deploymentId = deploymentId;
        _resourceName = resourceName;
    }

    public byte[]? Execute(ICommandContext context)
    {
        var deploymentManager = context.ProcessEngineConfiguration.DeploymentManager;
        if (deploymentManager == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No deployment found for id = '{_deploymentId}'",
                typeof(DeploymentEntity));

        var resourceEntity = deploymentManager.FindResourceByDeploymentIdAndResourceName(_deploymentId, _resourceName);
        if (resourceEntity?.Bytes == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No resource '{_resourceName}' found for deployment '{_deploymentId}'",
                typeof(DeploymentResource));

        return resourceEntity.Bytes;
    }

    public Task<byte[]?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

public class GetDeploymentResourceNamesCmd : ICommand<List<string>>
{
    private readonly string _deploymentId;

    public GetDeploymentResourceNamesCmd(string deploymentId)
    {
        if (string.IsNullOrWhiteSpace(deploymentId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("deploymentId is null");

        _deploymentId = deploymentId;
    }

    public List<string> Execute(ICommandContext context)
    {
        var deploymentManager = context.ProcessEngineConfiguration.DeploymentManager;
        if (deploymentManager == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No deployment found for id = '{_deploymentId}'",
                typeof(DeploymentEntity));

        var resources = deploymentManager.FindResourcesByDeploymentId(_deploymentId);
        if (resources.Count == 0)
            throw new WorkflowEngineObjectNotFoundException(
                $"No resources found for deployment '{_deploymentId}'",
                typeof(DeploymentResource));

        return resources.Select(r => r.Name).Where(n => n != null).Cast<string>().ToList();
    }

    public Task<List<string>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Core.Variable;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Cmd;

public class HasExecutionVariableCmd : ICommand<bool>
{
    private readonly string _executionId;
    private readonly string _variableName;
    private readonly bool _isLocal;

    public HasExecutionVariableCmd(string executionId, string variableName, bool isLocal)
    {
        _executionId = executionId;
        _variableName = variableName;
        _isLocal = isLocal;
    }

    public bool Execute(ICommandContext context) => throw new NotSupportedException("HasExecutionVariableCmd is async-only. Use ExecuteAsync.");

    public async Task<bool> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");
        if (string.IsNullOrEmpty(_variableName))
            throw new WorkflowEngineArgumentException("variableName is null");

        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        if (execution == null)
            return false;

        if (_isLocal)
        {
            return execution.Variables.ContainsKey(_variableName);
        }

        var current = execution;
        while (current != null)
        {
            if (current.Variables.ContainsKey(_variableName))
                return true;
            current = current.Parent;
        }
        return false;
    }
}

public class GetExecutionVariableCmd : ICommand<object?>
{
    private readonly string _executionId;
    private readonly string _variableName;
    private readonly bool _isLocal;

    public GetExecutionVariableCmd(string executionId, string variableName, bool isLocal)
    {
        _executionId = executionId;
        _variableName = variableName;
        _isLocal = isLocal;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("GetExecutionVariableCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");
        if (string.IsNullOrEmpty(_variableName))
            throw new WorkflowEngineArgumentException("variableName is null");

        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        if (execution == null)
            return null;

        return ExecuteInternal(execution);
    }

    protected object? ExecuteInternal(ExecutionEntity execution)
    {
        if (_isLocal)
        {
            return execution.GetVariableLocal(_variableName);
        }
        return execution.GetVariable(_variableName);
    }

}

public class GetExecutionVariableInstanceCmd : ICommand<VariableInstanceEntity?>
{
    private readonly string _executionId;
    private readonly string _variableName;
    private readonly bool _isLocal;

    public GetExecutionVariableInstanceCmd(string executionId, string variableName, bool isLocal)
    {
        _executionId = executionId;
        _variableName = variableName;
        _isLocal = isLocal;
    }

    public VariableInstanceEntity? Execute(ICommandContext context) => throw new NotSupportedException("GetExecutionVariableInstanceCmd is async-only. Use ExecuteAsync.");

    public async Task<VariableInstanceEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");
        if (string.IsNullOrEmpty(_variableName))
            throw new WorkflowEngineArgumentException("variableName is null");

        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        if (execution == null)
            throw new WorkflowEngineObjectNotFoundException($"execution {_executionId} doesn't exist", typeof(ExecutionEntity));

        return GetVariable(execution);
    }

    protected VariableInstanceEntity? GetVariable(ExecutionEntity execution)
    {
        object? value;
        if (_isLocal)
        {
            value = execution.GetVariableLocal(_variableName);
        }
        else
        {
            value = execution.GetVariable(_variableName);
        }

        if (value == null) return null;

        return new VariableInstanceEntity
        {
            Name = _variableName,
            Value = value,
            ExecutionId = execution.Id,
            ProcessInstanceId = execution.ProcessInstanceId
        };
    }

}

public class GetExecutionVariableInstancesCmd : ICommand<Dictionary<string, VariableInstanceEntity>>
{
    private readonly string _executionId;
    private readonly ICollection<string>? _variableNames;
    private readonly bool _isLocal;

    public GetExecutionVariableInstancesCmd(string executionId, ICollection<string>? variableNames, bool isLocal)
    {
        _executionId = executionId;
        _variableNames = variableNames;
        _isLocal = isLocal;
    }

    public Dictionary<string, VariableInstanceEntity> Execute(ICommandContext context) => throw new NotSupportedException("GetExecutionVariableInstancesCmd is async-only. Use ExecuteAsync.");

    public async Task<Dictionary<string, VariableInstanceEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");

        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        if (execution == null)
            throw new WorkflowEngineObjectNotFoundException($"execution {_executionId} doesn't exist", typeof(ExecutionEntity));

        return GetVariable(execution);
    }

    protected Dictionary<string, VariableInstanceEntity> GetVariable(ExecutionEntity execution)
    {
        var result = new Dictionary<string, VariableInstanceEntity>();
        var variables = new Dictionary<string, object?>();

        if (_variableNames == null || _variableNames.Count == 0)
        {
            if (_isLocal)
            {
                foreach (var kvp in execution.Variables)
                    variables[kvp.Key] = kvp.Value;
            }
            else
            {
                var current = execution;
                while (current != null)
                {
                    foreach (var kvp in current.Variables)
                    {
                        if (!variables.ContainsKey(kvp.Key))
                            variables[kvp.Key] = kvp.Value;
                    }
                    current = current.Parent;
                }
            }
        }
        else
        {
            foreach (var name in _variableNames)
            {
                var value = _isLocal ? execution.GetVariableLocal(name) : execution.GetVariable(name);
                if (value != null)
                    variables[name] = value;
            }
        }

        foreach (var kvp in variables)
        {
            result[kvp.Key] = new VariableInstanceEntity
            {
                Name = kvp.Key,
                Value = kvp.Value,
                ExecutionId = execution.Id,
                ProcessInstanceId = execution.ProcessInstanceId
            };
        }

        return result;
    }

}

public class GetExecutionsVariablesCmd : ICommand<Dictionary<string, object?>>
{
    private readonly ICollection<string> _executionIds;

    public GetExecutionsVariablesCmd(ICollection<string> executionIds)
    {
        _executionIds = executionIds;
    }

    public Dictionary<string, object?> Execute(ICommandContext context) => throw new NotSupportedException("GetExecutionsVariablesCmd is async-only. Use ExecuteAsync.");

    public async Task<Dictionary<string, object?>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_executionIds == null)
            throw new WorkflowEngineArgumentException("executionIds is null");
        if (_executionIds.Count == 0)
            throw new WorkflowEngineArgumentException("Set of executionIds is empty");

        var result = new Dictionary<string, object?>();

        foreach (var executionId in _executionIds)
        {
            try
            {
                var execution = await context.GetCurrentExecutionAsync(executionId, cancellationToken);
                if (execution != null)
                {
                    foreach (var kvp in execution.Variables)
                    {
                        if (!result.ContainsKey(kvp.Key))
                            result[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return result;
    }

}

public class GetProcessInstanceCommentsCmd : ICommand<List<CommentEntity>>
{
    private readonly string _processInstanceId;
    private readonly string? _type;

    public GetProcessInstanceCommentsCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId;
    }

    public GetProcessInstanceCommentsCmd(string processInstanceId, string? type)
    {
        _processInstanceId = processInstanceId;
        _type = type;
    }

    public List<CommentEntity> Execute(ICommandContext context) => throw new NotSupportedException("GetProcessInstanceCommentsCmd is async-only. Use ExecuteAsync.");

    public async Task<List<CommentEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");

        return (await context.FindCommentsAsync(c =>
                c.ProcessInstanceId == _processInstanceId &&
                (_type == null || c.Type == _type), cancellationToken)).ToList();
    }
}

public class GetProcessInstanceAttachmentsCmd : ICommand<List<AttachmentEntity>>
{
    private readonly string _processInstanceId;

    public GetProcessInstanceAttachmentsCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId;
    }

    public List<AttachmentEntity> Execute(ICommandContext context) => throw new NotSupportedException("GetProcessInstanceAttachmentsCmd is async-only. Use ExecuteAsync.");

    public async Task<List<AttachmentEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");

        return (await context.FindAttachmentsAsync(a => a.ProcessInstanceId == _processInstanceId, cancellationToken)).ToList();
    }
}

public class GetProcessInstanceEventsCmd : ICommand<List<EventEntity>>
{
    private readonly string _processInstanceId;

    public GetProcessInstanceEventsCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId;
    }

    public List<EventEntity> Execute(ICommandContext context) => throw new NotSupportedException("GetProcessInstanceEventsCmd is async-only. Use ExecuteAsync.");

    public async Task<List<EventEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");

        return (await context.FindCommentsAsync(c => c.ProcessInstanceId == _processInstanceId && c.Type == CommentEntity.TYPE_EVENT, cancellationToken))
            .Select(CommentEventMapper.ToEvent)
            .ToList();
    }
}

public class SetProcessInstanceNameCmd : ICommand<object?>
{
    private readonly string _processInstanceId;
    private readonly string? _name;

    public SetProcessInstanceNameCmd(string processInstanceId, string? name)
    {
        _processInstanceId = processInstanceId;
        _name = name;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("SetProcessInstanceNameCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");

        var execution = await context.GetCurrentExecutionAsync(_processInstanceId, cancellationToken);
        if (execution == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"process instance {_processInstanceId} doesn't exist",
                typeof(ExecutionEntity));

        if (!execution.IsProcessInstanceType)
            throw new WorkflowEngineObjectNotFoundException(
                $"process instance {_processInstanceId} doesn't exist, the given ID references an execution, though",
                typeof(ExecutionEntity));

        if (!execution.IsActive)
            throw new WorkflowEngineException($"process instance {_processInstanceId} is suspended, cannot set name");

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            await eventDispatcher.DispatchEventAsync(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.ENTITY_UPDATED,
                    new { ProcessInstanceId = _processInstanceId, Name = _name }), cancellationToken);
        }

        return null;
    }

}

public class SetProcessInstanceBusinessKeyCmd : ICommand<object?>
{
    private readonly string _processInstanceId;
    private readonly string _businessKey;

    public SetProcessInstanceBusinessKeyCmd(string processInstanceId, string businessKey)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new WorkflowEngineArgumentException(
                $"The process instance id is mandatory, but '{processInstanceId}' has been provided.");
        if (businessKey == null)
            throw new WorkflowEngineArgumentException("The business key is mandatory, but 'null' has been provided.");

        _processInstanceId = processInstanceId;
        _businessKey = businessKey;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("SetProcessInstanceBusinessKeyCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = await context.GetCurrentExecutionAsync(_processInstanceId, cancellationToken);
        if (execution == null)
        {
            throw new WorkflowEngineObjectNotFoundException(
                $"No process instance found for id = '{_processInstanceId}'.",
                typeof(ExecutionEntity));
        }

        if (!execution.IsProcessInstanceType)
        {
            throw new WorkflowEngineArgumentException(
                $"A process instance id is required, but the provided id '{_processInstanceId}' " +
                "points to a child execution. Please invoke with a root execution id.");
        }

        ExecuteInternal(context, execution);

        return null;
    }

    protected virtual void ExecuteInternal(ICommandContext context, ExecutionEntity processInstance)
    {
        processInstance.BusinessKey = _businessKey;
    }
}

public enum SuspensionState
{
    Active,
    Suspended
}

public abstract class AbstractSetProcessInstanceStateCmd : ICommand<object?>
{
    protected readonly string _processInstanceId;

    protected AbstractSetProcessInstanceStateCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId;
    }


    protected virtual void ExecuteInternal(ICommandContext context, ExecutionEntity execution)
    {
        var newState = GetNewState();
        var isActive = newState == SuspensionState.Active;

        foreach (var child in execution.ChildExecutions)
        {
            child.IsActive = isActive;
        }

        execution.IsActive = isActive;
    }

    protected abstract SuspensionState GetNewState();

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_processInstanceId))
            throw new WorkflowEngineArgumentException("ProcessInstanceId cannot be null.");

        var execution = await context.GetCurrentExecutionAsync(_processInstanceId, cancellationToken);
        if (execution == null)
            throw new WorkflowEngineObjectNotFoundException($"Cannot find processInstance for id '{_processInstanceId}'.", typeof(ExecutionEntity));
        if (!execution.IsProcessInstanceType)
            throw new WorkflowEngineException($"Cannot set suspension state for execution '{_processInstanceId}': not a process instance.");
        ExecuteInternal(context, execution);
        return null;
    }
}

public class GetEnabledActivitiesForAdhocSubProcessCmd : ICommand<List<BpmnModelNs.FlowNode>>
{
    private readonly string _executionId;

    public GetEnabledActivitiesForAdhocSubProcessCmd(string executionId)
    {
        _executionId = executionId;
    }

    public List<BpmnModelNs.FlowNode> Execute(ICommandContext context) => throw new NotSupportedException("GetEnabledActivitiesForAdhocSubProcessCmd is async-only. Use ExecuteAsync.");

    public async Task<List<BpmnModelNs.FlowNode>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        if (execution == null)
        {
            throw new WorkflowEngineObjectNotFoundException(
                $"No execution found for id '{_executionId}'",
                typeof(ExecutionEntity));
        }

        var enabledFlowNodes = new List<BpmnModelNs.FlowNode>();

        if (execution.CurrentFlowElement is BpmnModelNs.AdhocSubProcess adhocSubProcess)
        {
            if (adhocSubProcess.HasSequentialOrdering && execution.ChildExecutions.Count > 0)
            {
                return enabledFlowNodes;
            }

            foreach (var flowElement in adhocSubProcess.FlowElements)
            {
                if (flowElement is BpmnModelNs.FlowNode flowNode && flowNode.IncomingFlows.Count == 0)
                {
                    enabledFlowNodes.Add(flowNode);
                }
            }
        }
        else
        {
            throw new WorkflowEngineException(
                "The current flow element of the requested execution is not an ad-hoc sub process");
        }

        return enabledFlowNodes;
    }

}

public class ExecuteActivityForAdhocSubProcessCmd : ICommand<ExecutionEntity?>
{
    private readonly string _executionId;
    private readonly string _activityId;

    public ExecuteActivityForAdhocSubProcessCmd(string executionId, string activityId)
    {
        _executionId = executionId;
        _activityId = activityId;
    }

    public ExecutionEntity? Execute(ICommandContext context) => throw new NotSupportedException("ExecuteActivityForAdhocSubProcessCmd is async-only. Use ExecuteAsync.");

    public async Task<ExecutionEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        if (execution == null)
        {
            throw new WorkflowEngineObjectNotFoundException(
                $"No execution found for id '{_executionId}'",
                typeof(ExecutionEntity));
        }

        if (!(execution.CurrentFlowElement is BpmnModelNs.AdhocSubProcess adhocSubProcess))
        {
            throw new WorkflowEngineException(
                "The current flow element of the requested execution is not an ad-hoc sub process");
        }

        BpmnModelNs.FlowNode? foundNode = null;

        if (adhocSubProcess.HasSequentialOrdering && execution.ChildExecutions.Count > 0)
        {
            throw new WorkflowEngineException("Sequential ad-hoc sub process already has an active execution");
        }

        foreach (var flowElement in adhocSubProcess.FlowElements)
        {
            if (_activityId.Equals(flowElement.Id) && flowElement is BpmnModelNs.FlowNode flowNode)
            {
                if (flowNode.IncomingFlows.Count == 0)
                {
                    foundNode = flowNode;
                }
            }
        }

        if (foundNode == null)
        {
            throw new WorkflowEngineException($"The requested activity with id {_activityId} can not be enabled");
        }

        var activityExecution = new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid("N"),
            ProcessDefinitionId = execution.ProcessDefinitionId,
            ProcessInstanceId = execution.ProcessInstanceId,
            ParentId = execution.Id,
            CurrentFlowElementId = foundNode.Id,
            CurrentFlowElement = foundNode,
            IsActive = true,
            IsEnded = false,
            IsScope = false,
            IsConcurrent = true,
            IsProcessInstanceType = false,
            Variables = new Dictionary<string, object?>()
        };

        return activityExecution;
    }

}


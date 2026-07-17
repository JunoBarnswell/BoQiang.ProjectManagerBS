using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Core.Variable;

namespace AsterERP.Workflow.Core.Cmd;

public class SetTaskPriorityCmd : NeedsActiveTaskCmd<object?>
{
    private readonly int _priority;

    public SetTaskPriorityCmd(string taskId, int priority) : base(taskId)
    {
        _priority = priority;
    }

    protected override object? Execute(ICommandContext context, TaskImplementation task)
    {
        throw new NotSupportedException("SetTaskPriorityCmd is async-only. Use ExecuteAsync.");
    }

    public override object? Execute(ICommandContext context) =>
        throw new NotSupportedException("SetTaskPriorityCmd is async-only. Use ExecuteAsync.");

    protected override async Task<object?> ExecuteAsync(
        ICommandContext context,
        TaskImplementation task,
        CancellationToken cancellationToken)
    {
        await TaskCommandHelper.UpdateTaskAsync(
            context, TaskId, current => current with { Priority = _priority }, cancellationToken);

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.ENTITY_UPDATED,
                    new { TaskId = TaskId, Priority = _priority }));
        }

        return null;
    }

    protected override string GetSuspendedTaskException() => "Cannot set priority on a suspended task";
}

public class SetTaskDueDateCmd : NeedsActiveTaskCmd<object?>
{
    private readonly DateTime? _dueDate;

    public SetTaskDueDateCmd(string taskId, DateTime? dueDate) : base(taskId)
    {
        _dueDate = dueDate;
    }

    protected override object? Execute(ICommandContext context, TaskImplementation task)
    {
        throw new NotSupportedException("SetTaskDueDateCmd is async-only. Use ExecuteAsync.");
    }

    public override object? Execute(ICommandContext context) =>
        throw new NotSupportedException("SetTaskDueDateCmd is async-only. Use ExecuteAsync.");

    protected override async Task<object?> ExecuteAsync(
        ICommandContext context,
        TaskImplementation task,
        CancellationToken cancellationToken)
    {
        await TaskCommandHelper.UpdateTaskAsync(
            context, TaskId, current => current with { DueDate = _dueDate }, cancellationToken);

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.ENTITY_UPDATED,
                    new { TaskId = TaskId, DueDate = _dueDate }));
        }

        return null;
    }

    protected override string GetSuspendedTaskException() => "Cannot set due date on a suspended task";
}

public class HasTaskVariableCmd : ICommand<bool>
{
    private readonly string _taskId;
    private readonly string _variableName;
    private readonly bool _isLocal;

    public HasTaskVariableCmd(string taskId, string variableName, bool isLocal)
    {
        _taskId = taskId;
        _variableName = variableName;
        _isLocal = isLocal;
    }

    public bool Execute(ICommandContext context) =>
        throw new NotSupportedException("HasTaskVariableCmd is async-only. Use ExecuteAsync.");

    public async Task<bool> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");
        if (string.IsNullOrEmpty(_variableName))
            throw new WorkflowEngineArgumentException("variableName is null");

        try
        {
            var execution = await context.FindExecutionByTaskIdAsync(_taskId, cancellationToken);
            if (execution == null)
                throw new WorkflowEngineObjectNotFoundException($"task {_taskId} doesn't exist", typeof(TaskImplementation));

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
        catch (ArgumentException)
        {
            return false;
        }
    }

}

public class GetTaskVariableCmd : ICommand<object?>
{
    private readonly string _taskId;
    private readonly string _variableName;
    private readonly bool _isLocal;

    public GetTaskVariableCmd(string taskId, string variableName, bool isLocal)
    {
        _taskId = taskId;
        _variableName = variableName;
        _isLocal = isLocal;
    }

    public object? Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTaskVariableCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");
        if (string.IsNullOrEmpty(_variableName))
            throw new WorkflowEngineArgumentException("variableName is null");

        try
        {
            var execution = await context.FindExecutionByTaskIdAsync(_taskId, cancellationToken);
            if (execution == null)
                throw new WorkflowEngineObjectNotFoundException($"task {_taskId} doesn't exist", typeof(TaskImplementation));

            if (_isLocal)
            {
                return execution.GetVariableLocal(_variableName);
            }

            return execution.GetVariable(_variableName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

}

public class GetTaskVariableInstanceCmd : ICommand<VariableInstanceEntity?>
{
    private readonly string _taskId;
    private readonly string _variableName;
    private readonly bool _isLocal;

    public GetTaskVariableInstanceCmd(string taskId, string variableName, bool isLocal)
    {
        _taskId = taskId;
        _variableName = variableName;
        _isLocal = isLocal;
    }

    public VariableInstanceEntity? Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTaskVariableInstanceCmd is async-only. Use ExecuteAsync.");

    public async Task<VariableInstanceEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");
        if (string.IsNullOrEmpty(_variableName))
            throw new WorkflowEngineArgumentException("variableName is null");

        try
        {
            var execution = await context.FindExecutionByTaskIdAsync(_taskId, cancellationToken);
            if (execution == null)
                throw new WorkflowEngineObjectNotFoundException($"task {_taskId} doesn't exist", typeof(TaskImplementation));

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
                ExecutionId = execution.Id,
                ProcessInstanceId = execution.ProcessInstanceId,
                TaskId = _taskId
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

}

public class GetTaskVariableInstancesCmd : ICommand<Dictionary<string, VariableInstanceEntity>>
{
    private readonly string _taskId;
    private readonly ICollection<string>? _variableNames;
    private readonly bool _isLocal;

    public GetTaskVariableInstancesCmd(string taskId, ICollection<string>? variableNames, bool isLocal)
    {
        _taskId = taskId;
        _variableNames = variableNames;
        _isLocal = isLocal;
    }

    public Dictionary<string, VariableInstanceEntity> Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTaskVariableInstancesCmd is async-only. Use ExecuteAsync.");

    public async Task<Dictionary<string, VariableInstanceEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        var result = new Dictionary<string, VariableInstanceEntity>();

        try
        {
            var execution = await context.FindExecutionByTaskIdAsync(_taskId, cancellationToken);
            if (execution == null)
                throw new WorkflowEngineObjectNotFoundException($"task {_taskId} doesn't exist", typeof(TaskImplementation));

            var variables = new Dictionary<string, object?>();

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

            foreach (var kvp in variables)
            {
                if (_variableNames == null || _variableNames.Contains(kvp.Key))
                {
                    result[kvp.Key] = new VariableInstanceEntity
                    {
                        Name = kvp.Key,
                        ExecutionId = execution.Id,
                        ProcessInstanceId = execution.ProcessInstanceId,
                        TaskId = _taskId
                    };
                }
            }
        }
        catch (ArgumentException)
        {
        }

        return result;
    }

}

public class GetTasksLocalVariablesCmd : ICommand<List<VariableInstanceEntity>>
{
    private readonly ICollection<string> _taskIds;

    public GetTasksLocalVariablesCmd(ICollection<string> taskIds)
    {
        _taskIds = taskIds;
    }

    public List<VariableInstanceEntity> Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTasksLocalVariablesCmd is async-only. Use ExecuteAsync.");

    public async Task<List<VariableInstanceEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (_taskIds == null)
            throw new WorkflowEngineArgumentException("taskIds is null");
        if (_taskIds.Count == 0)
            throw new WorkflowEngineArgumentException("Set of taskIds is empty");

        var instances = new List<VariableInstanceEntity>();

        foreach (var taskId in _taskIds)
        {
            try
            {
                var execution = await context.FindExecutionByTaskIdAsync(taskId, cancellationToken);
                if (execution != null)
                {
                    foreach (var kvp in execution.Variables)
                    {
                        instances.Add(new VariableInstanceEntity
                        {
                            Name = kvp.Key,
                            ExecutionId = execution.Id,
                            ProcessInstanceId = execution.ProcessInstanceId,
                            TaskId = taskId
                        });
                    }
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return instances;
    }

}

public class EventEntity
{
    public string Id { get; set; } = AbpTimeIdProvider.NewGuid("N");
    public string? Action { get; set; }
    public string? Message { get; set; }
    public string? UserId { get; set; }
    public string? TaskId { get; set; }
    public string? ProcessInstanceId { get; set; }
    public DateTime Time { get; set; } = AbpTimeIdProvider.UtcNow;
    public string? Type { get; set; }
}

public class GetTaskEventsCmd : ICommand<List<EventEntity>>
{
    private readonly string _taskId;

    public GetTaskEventsCmd(string taskId)
    {
        _taskId = taskId;
    }

    public List<EventEntity> Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTaskEventsCmd is async-only. Use ExecuteAsync.");

    public async Task<List<EventEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        return (await context.FindCommentsAsync(
                c => c.TaskId == _taskId && c.Type == CommentEntity.TYPE_EVENT,
                cancellationToken))
            .Select(CommentEventMapper.ToEvent)
            .ToList();
    }
}

public class GetTaskEventCmd : ICommand<EventEntity?>
{
    private readonly string _eventId;

    public GetTaskEventCmd(string eventId)
    {
        _eventId = eventId ?? throw new WorkflowEngineArgumentException("eventId is null");
    }

    public EventEntity? Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTaskEventCmd is async-only. Use ExecuteAsync.");

    public async Task<EventEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return (await context.FindCommentsAsync(
                c => c.Id == _eventId && c.Type == CommentEntity.TYPE_EVENT,
                cancellationToken))
            .Select(CommentEventMapper.ToEvent)
            .FirstOrDefault();
    }
}

public class CommentEntity
{
    public string Id { get; set; } = AbpTimeIdProvider.NewGuid("N");
    public string? UserId { get; set; }
    public DateTime Time { get; set; } = AbpTimeIdProvider.UtcNow;
    public string? TaskId { get; set; }
    public string? ProcessInstanceId { get; set; }
    public string? Type { get; set; }
    public string? Message { get; set; }
    public string? FullMessage { get; set; }
    public string? Action { get; set; }
    public const string TYPE_COMMENT = "comment";
    public const string TYPE_EVENT = "event";
}

public class GetTaskCommentsCmd : ICommand<List<CommentEntity>>
{
    protected readonly string _taskId;

    public GetTaskCommentsCmd(string taskId)
    {
        _taskId = taskId;
    }

    public virtual List<CommentEntity> Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTaskCommentsCmd is async-only. Use ExecuteAsync.");

    public virtual async Task<List<CommentEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        return (await context.FindCommentsAsync(c => c.TaskId == _taskId, cancellationToken))
            .ToList();
    }
}

public class GetTaskCommentsByTypeCmd : GetTaskCommentsCmd
{
    private readonly string _type;

    public GetTaskCommentsByTypeCmd(string taskId, string type) : base(taskId)
    {
        _type = type;
    }

    public override List<CommentEntity> Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTaskCommentsByTypeCmd is async-only. Use ExecuteAsync.");

    public override async Task<List<CommentEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");
        if (string.IsNullOrEmpty(_type))
            throw new WorkflowEngineArgumentException("type is null");

        return (await context.FindCommentsAsync(
                c => c.TaskId == _taskId && c.Type == _type,
                cancellationToken))
            .ToList();
    }
}

public class GetTypeCommentsCmd : ICommand<List<CommentEntity>>
{
    private readonly string _type;

    public GetTypeCommentsCmd(string type)
    {
        _type = type;
    }

    public List<CommentEntity> Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTypeCommentsCmd is async-only. Use ExecuteAsync.");

    public async Task<List<CommentEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_type))
            throw new WorkflowEngineArgumentException("type is null");

        return (await context.FindCommentsAsync(c => c.Type == _type, cancellationToken))
            .ToList();
    }
}

public class AttachmentEntity
{
    public string Id { get; set; } = AbpTimeIdProvider.NewGuid("N");
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? TaskId { get; set; }
    public string? ProcessInstanceId { get; set; }
    public string? UserId { get; set; }
    public DateTime Time { get; set; } = AbpTimeIdProvider.UtcNow;
    public string? Url { get; set; }
    public string? ContentId { get; set; }
}

public class GetTaskAttachmentsCmd : ICommand<List<AttachmentEntity>>
{
    private readonly string _taskId;

    public GetTaskAttachmentsCmd(string taskId)
    {
        _taskId = taskId;
    }

    public List<AttachmentEntity> Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTaskAttachmentsCmd is async-only. Use ExecuteAsync.");

    public async Task<List<AttachmentEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        return (await context.FindAttachmentsAsync(a => a.TaskId == _taskId, cancellationToken))
            .ToList();
    }
}

public class DataObjectImpl
{
    public string? Name { get; }
    public object? Value { get; }
    public string? Documentation { get; }
    public string? Type { get; }
    public string? LocalizedName { get; }
    public string? LocalizedDescription { get; }
    public string? DataObjectId { get; }

    public DataObjectImpl(string? name, object? value, string? documentation, string? type,
        string? localizedName, string? localizedDescription, string? dataObjectId)
    {
        Name = name;
        Value = value;
        Documentation = documentation;
        Type = type;
        LocalizedName = localizedName;
        LocalizedDescription = localizedDescription;
        DataObjectId = dataObjectId;
    }
}

public class GetTaskDataObjectCmd : ICommand<DataObjectImpl?>
{
    private readonly string _taskId;
    private readonly string _variableName;
    private readonly string? _locale;
    private readonly bool _withLocalizationFallback;

    public GetTaskDataObjectCmd(string taskId, string variableName)
    {
        _taskId = taskId;
        _variableName = variableName;
    }

    public GetTaskDataObjectCmd(string taskId, string variableName, string? locale, bool withLocalizationFallback)
    {
        _taskId = taskId;
        _variableName = variableName;
        _locale = locale;
        _withLocalizationFallback = withLocalizationFallback;
    }

    public DataObjectImpl? Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTaskDataObjectCmd is async-only. Use ExecuteAsync.");

    public async Task<DataObjectImpl?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");
        if (string.IsNullOrEmpty(_variableName))
            throw new WorkflowEngineArgumentException("variableName is null");

        try
        {
            var execution = await context.FindExecutionByTaskIdAsync(_taskId, cancellationToken);
            if (execution == null)
                throw new WorkflowEngineObjectNotFoundException($"task {_taskId} doesn't exist", typeof(TaskImplementation));

            var variableEntity = execution.GetVariable(_variableName);
            if (variableEntity == null) return null;

            return new DataObjectImpl(
                _variableName,
                variableEntity,
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

public class GetTaskDataObjectsCmd : ICommand<Dictionary<string, DataObjectImpl>>
{
    private readonly string _taskId;
    private readonly ICollection<string>? _variableNames;
    private readonly string? _locale;
    private readonly bool _withLocalizationFallback;

    public GetTaskDataObjectsCmd(string taskId, ICollection<string>? variableNames)
    {
        _taskId = taskId;
        _variableNames = variableNames;
    }

    public GetTaskDataObjectsCmd(string taskId, ICollection<string>? variableNames, string? locale, bool withLocalizationFallback)
    {
        _taskId = taskId;
        _variableNames = variableNames;
        _locale = locale;
        _withLocalizationFallback = withLocalizationFallback;
    }

    public Dictionary<string, DataObjectImpl> Execute(ICommandContext context) =>
        throw new NotSupportedException("GetTaskDataObjectsCmd is async-only. Use ExecuteAsync.");

    public async Task<Dictionary<string, DataObjectImpl>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        var result = new Dictionary<string, DataObjectImpl>();

        try
        {
            var execution = await context.FindExecutionByTaskIdAsync(_taskId, cancellationToken);
            if (execution == null)
                throw new WorkflowEngineObjectNotFoundException($"task {_taskId} doesn't exist", typeof(TaskImplementation));

            var variables = execution.Variables;

            foreach (var kvp in variables)
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

public class GetSubTasksCmd : ICommand<List<TaskImplementation>>
{
    private readonly string _parentTaskId;

    public GetSubTasksCmd(string parentTaskId)
    {
        _parentTaskId = parentTaskId;
    }

    public List<TaskImplementation> Execute(ICommandContext context) =>
        throw new NotSupportedException("GetSubTasksCmd is async-only. Use ExecuteAsync.");

    public async Task<List<TaskImplementation>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_parentTaskId))
            throw new WorkflowEngineArgumentException("parentTaskId is null");

        return (await context.FindTasksAsync(
                task => task.ParentTaskId == _parentTaskId,
                cancellationToken))
            .ToList();
    }
}

public abstract class AbstractCompleteTaskCmd : NeedsActiveTaskCmd<object?>
{
    protected AbstractCompleteTaskCmd(string taskId) : base(taskId)
    {
    }

    protected async Task ExecuteTaskCompleteAsync(
        ICommandContext context,
        TaskImplementation task,
        Dictionary<string, object?>? variables,
        bool localScope,
        CancellationToken cancellationToken)
    {
        if (task.DelegationState == "PENDING")
        {
            throw new WorkflowEngineException("A delegated task cannot be completed, but should be resolved instead.");
        }

        var execution = !string.IsNullOrEmpty(task.Id)
            ? await context.FindExecutionByTaskIdAsync(task.Id, cancellationToken)
            : (!string.IsNullOrEmpty(task.ProcessInstanceId)
                ? await context.GetCurrentExecutionAsync(task.ProcessInstanceId, cancellationToken)
                : null);

        if (execution != null)
        {
            context.ProcessEngineConfiguration.HistoryManager.RecordTaskCompleted(execution, task, null);
            if (!localScope && variables != null)
            {
                foreach (var variable in variables)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    context.ProcessEngineConfiguration.HistoryManager.RecordVariable(execution, variable.Key, variable.Value, task.Id);
                }
            }
        }

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateTaskCompletedEvent(task.Id, task.ProcessInstanceId ?? ""));
        }

        if (!string.IsNullOrEmpty(task.Id))
        {
            context.DeleteTask(task.Id);
        }
    }
}

public class CompleteAdhocSubProcessCmd : ICommand<object?>
{
    private readonly string _executionId;

    public CompleteAdhocSubProcessCmd(string executionId)
    {
        _executionId = executionId;
    }

    public object? Execute(ICommandContext context) =>
        throw new NotSupportedException("CompleteAdhocSubProcessCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");

        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        if (execution == null)
        {
            throw new WorkflowEngineObjectNotFoundException(
                $"No execution found for id '{_executionId}'",
                typeof(Execution.ExecutionEntity));
        }

        if (execution.ChildExecutions.Count > 0)
        {
            throw new WorkflowEngineException(
                "Ad-hoc sub process has running child executions that need to be completed first");
        }

        return null;
    }

}


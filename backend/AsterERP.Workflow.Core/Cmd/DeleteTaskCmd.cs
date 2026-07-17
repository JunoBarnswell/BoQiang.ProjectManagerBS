using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class DeleteTaskCmd : ICommand<object?>
{
    private readonly string _taskId;
    private readonly bool _cascade;
    private readonly string? _deleteReason;

    public DeleteTaskCmd(string taskId, bool cascade = false, string? deleteReason = null)
    {
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        _cascade = cascade;
        _deleteReason = deleteReason;
    }

    public DeleteTaskCmd(string taskId, string? deleteReason, bool cascade)
    {
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        _deleteReason = deleteReason;
        _cascade = cascade;
    }

    public object? Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        context.DeleteTask(_taskId);

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.ENTITY_DELETED,
                    new { TaskId = _taskId, DeleteReason = _deleteReason }));
        }

        return null;
    }

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

public class DeleteTasksCmd : ICommand<object?>
{
    private readonly ICollection<string> _taskIds;
    private readonly string? _deleteReason;
    private readonly bool _cascade;

    public DeleteTasksCmd(ICollection<string> taskIds, string? deleteReason = null, bool cascade = false)
    {
        _taskIds = taskIds ?? throw new ArgumentNullException(nameof(taskIds));
        _deleteReason = deleteReason;
        _cascade = cascade;
    }

    public object? Execute(ICommandContext context)
    {
        if (_taskIds == null || _taskIds.Count == 0)
            throw new WorkflowEngineArgumentException("taskIds is null or empty");

        foreach (var taskId in _taskIds)
        {
            context.DeleteTask(taskId);
        }

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            foreach (var taskId in _taskIds)
            {
                eventDispatcher.DispatchEvent(
                    WorkflowEventBuilder.CreateEntityEvent(
                        WorkflowEventType.ENTITY_DELETED,
                        new { TaskId = taskId, DeleteReason = _deleteReason }));
            }
        }

        return null;
    }

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

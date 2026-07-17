using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class GetTaskByIdCmd : ICommand<TaskImplementation?>
{
    private readonly string _taskId;

    public GetTaskByIdCmd(string taskId)
    {
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
    }


    public async Task<TaskImplementation?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        var tasks = await context.FindTasksAsync(task => task.Id == _taskId, cancellationToken);
        return tasks.FirstOrDefault();
    }
}

public class GetAllTasksCmd : ICommand<List<TaskImplementation>>
{

    public async Task<List<TaskImplementation>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return (await context.FindTasksAsync(cancellationToken: cancellationToken)).ToList();
    }
}

public class GetTasksByAssigneeCmd : ICommand<List<TaskImplementation>>
{
    private readonly string _assignee;

    public GetTasksByAssigneeCmd(string assignee)
    {
        _assignee = assignee ?? throw new ArgumentNullException(nameof(assignee));
    }


    public async Task<List<TaskImplementation>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_assignee))
            throw new WorkflowEngineArgumentException("assignee is null");

        return (await context.FindTasksAsync(task => task.Assignee == _assignee, cancellationToken)).ToList();
    }
}

public class GetTasksByProcessInstanceCmd : ICommand<List<TaskImplementation>>
{
    private readonly string _processInstanceId;

    public GetTasksByProcessInstanceCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId ?? throw new ArgumentNullException(nameof(processInstanceId));
    }


    public async Task<List<TaskImplementation>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");

        return (await context.FindTasksAsync(task => task.ProcessInstanceId == _processInstanceId, cancellationToken)).ToList();
    }
}

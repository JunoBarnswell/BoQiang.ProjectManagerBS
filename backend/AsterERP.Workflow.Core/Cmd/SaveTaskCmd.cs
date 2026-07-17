using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class SaveTaskCmd : ICommand<TaskImplementation>
{
    private readonly TaskImplementation _task;

    public SaveTaskCmd(TaskImplementation task)
    {
        _task = task ?? throw new ArgumentNullException(nameof(task));
    }

    public TaskImplementation Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_task.Id))
            throw new WorkflowEngineArgumentException("Task id is null");

        context.SaveTask(_task);
        return _task;
    }

    public Task<TaskImplementation> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

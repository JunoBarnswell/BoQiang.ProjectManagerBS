using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class DelegateTaskCmd : NeedsActiveTaskCmd<object?>
{
    private readonly string _userId;

    public DelegateTaskCmd(string taskId, string userId) : base(taskId)
    {
        _userId = userId ?? throw new ArgumentNullException(nameof(userId));
    }

    protected override object? Execute(ICommandContext context, TaskImplementation task)
    {
        TaskCommandHelper.UpdateTask(context, TaskId, current => current with
        {
            Owner = current.Assignee,
            Assignee = _userId,
            DelegationState = "PENDING"
        });

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateTaskAssignedEvent(TaskId, _userId, task.ProcessInstanceId ?? ""));
        }

        return null;
    }

    protected override string GetSuspendedTaskException() => "Cannot delegate a suspended task";
}

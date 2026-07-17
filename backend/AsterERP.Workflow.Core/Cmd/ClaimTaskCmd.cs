using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class ClaimTaskCmd : NeedsActiveTaskCmd<object?>
{
    private readonly string? _userId;

    public ClaimTaskCmd(string taskId, string? userId) : base(taskId)
    {
        _userId = userId;
    }

    protected override object? Execute(ICommandContext context, TaskImplementation task)
    {
        if (_userId != null)
        {
            if (task.Assignee != null && !task.Assignee.Equals(_userId))
            {
                throw new WorkflowTaskAlreadyClaimedException(
                    $"Task '{TaskId}' is already claimed by someone else.");
            }
        }

        TaskCommandHelper.UpdateTask(context, TaskId, current => current with { Assignee = _userId });

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled && _userId != null)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateTaskAssignedEvent(TaskId, _userId, task.ProcessInstanceId ?? ""));
        }

        return null;
    }

    protected override string GetSuspendedTaskException() => "Cannot claim a suspended task";
}

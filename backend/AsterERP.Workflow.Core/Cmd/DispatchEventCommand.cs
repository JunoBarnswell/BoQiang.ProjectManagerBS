using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Cmd;

public class DispatchEventCommand : ICommand<object?>
{
    private readonly IWorkflowEvent? _event;

    public DispatchEventCommand(IWorkflowEvent @event)
    {
        _event = @event;
    }

    public object? Execute(ICommandContext context)
    {
        if (_event == null)
            throw new WorkflowEngineArgumentException("event is null");

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(_event);
        }
        else
        {
            throw new WorkflowEngineException("Event dispatcher is disabled, cannot dispatch event");
        }

        return null;
    }

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

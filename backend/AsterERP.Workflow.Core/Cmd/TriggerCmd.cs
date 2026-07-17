using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Agenda;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Cmd;

public class TriggerCmd : NeedsActiveExecutionCmd<object?>
{
    private readonly Dictionary<string, object?>? _processVariables;

    public TriggerCmd(string executionId, Dictionary<string, object?>? processVariables) : base(executionId)
    {
        _processVariables = processVariables;
    }

    protected override object? Execute(ICommandContext context, ExecutionEntity execution) =>
        throw new NotSupportedException("TriggerCmd is async-only. Use ExecuteAsync.");

    protected override async Task<object?> ExecuteAsync(
        ICommandContext context,
        ExecutionEntity execution,
        CancellationToken cancellationToken)
    {
        if (_processVariables != null)
        {
            foreach (var kvp in _processVariables)
                execution.SetVariable(kvp.Key, kvp.Value);
        }

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateCustomEvent("ACTIVITY_SIGNALED",
                    new Dictionary<string, object?>
                    {
                        ["executionId"] = execution.Id,
                        ["activityId"] = execution.CurrentActivityId
                    }));
        }

        var agenda = new WorkflowEngineAgendaFactory(context.ProcessEngineConfiguration).CreateAgenda();
        agenda.PlanTriggerExecutionOperation(execution);
        while (!agenda.IsEmpty)
        {
            await agenda.ExecuteNextAsync(cancellationToken);
        }

        return null;
    }

    protected override void ValidateExecutionState(ExecutionEntity execution)
    {
        if (execution.IsEnded)
        {
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Cannot trigger an execution that is ended");
        }
    }

    protected override string GetSuspendedExceptionMessage() => "Cannot trigger an execution that is suspended";
}

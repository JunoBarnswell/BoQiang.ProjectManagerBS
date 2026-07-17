using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Cmd;

public class SetTaskVariablesCmd : ICommand<object?>
{
    private readonly string _taskId;
    private readonly Dictionary<string, object?> _variables;
    private readonly bool _localScope;

    public SetTaskVariablesCmd(string taskId, Dictionary<string, object?> variables, bool localScope = false)
    {
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _localScope = localScope;
    }

    public object? Execute(ICommandContext context) =>
        throw new NotSupportedException("SetTaskVariablesCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        if (!_localScope)
        {
            try
            {
                var execution = await context.FindExecutionByTaskIdAsync(_taskId, cancellationToken);
                if (execution == null)
                    throw new WorkflowEngineObjectNotFoundException($"task {_taskId} doesn't exist", typeof(Services.TaskImplementation));

                foreach (var kvp in _variables)
                {
                    execution.SetVariable(kvp.Key, kvp.Value);
                    var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
                    if (eventDispatcher.IsEnabled)
                    {
                        eventDispatcher.DispatchEvent(
                            WorkflowEventBuilder.CreateVariableCreatedEvent(
                                kvp.Key, kvp.Value, execution.Id, execution.ProcessInstanceId ?? ""));
                    }
                }
            }
            catch (ArgumentException)
            {
            }
        }
        else
        {
            try
            {
                var execution = await context.FindExecutionByTaskIdAsync(_taskId, cancellationToken);
                if (execution == null)
                    throw new WorkflowEngineObjectNotFoundException($"task {_taskId} doesn't exist", typeof(Services.TaskImplementation));

                foreach (var kvp in _variables)
                {
                    execution.SetVariableLocal(kvp.Key, kvp.Value);
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return null;
    }

}

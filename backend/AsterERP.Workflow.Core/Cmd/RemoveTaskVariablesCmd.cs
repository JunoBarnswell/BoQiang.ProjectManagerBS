using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Cmd;

public class RemoveTaskVariablesCmd : ICommand<object?>
{
    private readonly string _taskId;
    private readonly ICollection<string> _variableNames;
    private readonly bool _localScope;

    public RemoveTaskVariablesCmd(string taskId, ICollection<string> variableNames, bool localScope = false)
    {
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        _variableNames = variableNames ?? throw new ArgumentNullException(nameof(variableNames));
        _localScope = localScope;
    }

    public object? Execute(ICommandContext context) =>
        throw new NotSupportedException("RemoveTaskVariablesCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        try
        {
            var execution = await context.GetCurrentExecutionAsync(_taskId, cancellationToken);
            foreach (var varName in _variableNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (execution.Variables.ContainsKey(varName))
                {
                    execution.Variables.Remove(varName);
                    var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
                    if (eventDispatcher.IsEnabled)
                    {
                        eventDispatcher.DispatchEvent(
                            WorkflowEventBuilder.CreateVariableDeletedEvent(
                                varName, execution.Id, execution.ProcessInstanceId ?? ""));
                    }
                }
            }
        }
        catch (ArgumentException)
        {
        }

        return null;
    }
}

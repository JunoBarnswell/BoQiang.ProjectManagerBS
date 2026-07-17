using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Cmd;

public class RemoveExecutionVariablesCmd : ICommand<object?>
{
    private readonly string _executionId;
    private readonly ICollection<string> _variableNames;
    private readonly bool _localScope;

    public RemoveExecutionVariablesCmd(string executionId, ICollection<string> variableNames, bool localScope = false)
    {
        _executionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        _variableNames = variableNames ?? throw new ArgumentNullException(nameof(variableNames));
        _localScope = localScope;
    }

    public object? Execute(ICommandContext context) =>
        throw new NotSupportedException("RemoveExecutionVariablesCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");

        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);

        foreach (var varName in _variableNames)
        {
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

        return null;
    }

}

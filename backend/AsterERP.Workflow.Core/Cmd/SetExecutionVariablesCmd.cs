using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
namespace AsterERP.Workflow.Core.Cmd;

public class SetExecutionVariablesCmd : ICommand<object?>
{
    private readonly string _executionId;
    private readonly Dictionary<string, object?> _variables;
    private readonly bool _localScope;

    public SetExecutionVariablesCmd(string executionId, Dictionary<string, object?> variables, bool localScope = false)
    {
        _executionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _localScope = localScope;
    }

    public object? Execute(ICommandContext context) =>
        throw new NotSupportedException("SetExecutionVariablesCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");

        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        if (execution == null)
        {
            throw new WorkflowEngineObjectNotFoundException(
                $"No execution found for id '{_executionId}'",
                typeof(Execution.ExecutionEntity));
        }

        var processInstanceId = execution.ProcessInstanceId ?? execution.Id;
        ProcessInstanceUpdateGuard.Enter(processInstanceId);
        context.AddCloseListener(ProcessInstanceUpdateGuard.CreateReleaseListener(processInstanceId));

        foreach (var kvp in _variables)
        {
            if (_localScope)
                execution.SetVariableLocal(kvp.Key, kvp.Value);
            else
                execution.SetVariable(kvp.Key, kvp.Value);

            context.ProcessEngineConfiguration.HistoryManager.RecordVariable(
                execution,
                kvp.Key,
                kvp.Value,
                taskId: null);

            var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
            if (eventDispatcher.IsEnabled)
            {
                eventDispatcher.DispatchEvent(
                    WorkflowEventBuilder.CreateVariableCreatedEvent(
                        kvp.Key, kvp.Value, execution.Id, execution.ProcessInstanceId ?? ""));
            }
        }

        return null;
    }

}

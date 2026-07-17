using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Cmd;

public class StartCreatedProcessInstanceCmd : ICommand<ProcessInstanceResult>
{
    private readonly string _processInstanceId;
    private readonly Dictionary<string, object?>? _variables;

    public StartCreatedProcessInstanceCmd(string processInstanceId, Dictionary<string, object?>? variables = null)
    {
        _processInstanceId = processInstanceId ?? throw new ArgumentNullException(nameof(processInstanceId));
        _variables = variables;
    }

    public ProcessInstanceResult Execute(ICommandContext context) =>
        throw new NotSupportedException("StartCreatedProcessInstanceCmd is async-only. Use ExecuteAsync.");

    private static ProcessInstanceResult CreateResult(ExecutionEntity execution) => new()
    {
        Id = execution.Id,
        ProcessDefinitionId = execution.ProcessDefinitionId,
        ProcessInstanceId = execution.ProcessInstanceId ?? execution.Id,
        BusinessKey = execution.BusinessKey,
        IsStarted = true,
        IsEnded = execution.IsEnded
    };

    public async Task<ProcessInstanceResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var execution = await context.GetCurrentExecutionAsync(_processInstanceId, cancellationToken);

        if (execution.IsActive)
            throw new WorkflowEngineException($"Process instance '{_processInstanceId}' is already started");

        if (_variables != null)
        {
            foreach (var kvp in _variables)
                execution.SetVariable(kvp.Key, kvp.Value);
        }

        execution.IsActive = true;

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateProcessStartedEvent(
                    execution.ProcessInstanceId ?? execution.Id,
                    execution.ProcessDefinitionId ?? "",
                    execution.BusinessKey));
        }

        return CreateResult(execution);
    }
}

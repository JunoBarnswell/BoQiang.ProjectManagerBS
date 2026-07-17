using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Cmd;

public class DeleteProcessInstanceCmd : ICommand<object?>
{
    private readonly string _processInstanceId;
    private readonly string? _deleteReason;

    public DeleteProcessInstanceCmd(string processInstanceId, string? deleteReason = null)
    {
        _processInstanceId = processInstanceId ?? throw new ArgumentNullException(nameof(processInstanceId));
        _deleteReason = deleteReason;
    }

    public object? Execute(ICommandContext context) =>
        throw new NotSupportedException("DeleteProcessInstanceCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");

        var execution = await context.GetCurrentExecutionAsync(_processInstanceId, cancellationToken);
        if (execution == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No process instance found for id = '{_processInstanceId}'",
                typeof(Execution.ExecutionEntity));

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.PROCESS_CANCELLED,
                    new { ProcessInstanceId = _processInstanceId, DeleteReason = _deleteReason },
                    processInstanceId: _processInstanceId,
                    processDefinitionId: execution.ProcessDefinitionId));
        }

        context.RemoveExecution(_processInstanceId);

        return null;
    }

}

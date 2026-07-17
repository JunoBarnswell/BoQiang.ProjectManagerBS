using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Cmd;

public class ActivateProcessInstanceCmd : ICommand<object?>
{
    private readonly string _processInstanceId;

    public ActivateProcessInstanceCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId ?? throw new ArgumentNullException(nameof(processInstanceId));
    }

    public object? Execute(ICommandContext context) =>
        throw new NotSupportedException("ActivateProcessInstanceCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");

        var execution = await context.GetCurrentExecutionAsync(_processInstanceId, cancellationToken);
        if (execution == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No process instance found for id = '{_processInstanceId}'",
                typeof(Execution.ExecutionEntity));

        execution.IsActive = true;
        return null;
    }
}

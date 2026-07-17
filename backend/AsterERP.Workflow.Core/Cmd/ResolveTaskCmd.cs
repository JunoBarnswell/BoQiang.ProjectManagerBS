using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class ResolveTaskCmd : NeedsActiveTaskCmd<object?>
{
    private readonly Dictionary<string, object?>? _variables;

    public ResolveTaskCmd(string taskId, Dictionary<string, object?>? variables) : base(taskId)
    {
        _variables = variables;
    }

    public override object? Execute(ICommandContext context) =>
        throw new NotSupportedException("ResolveTaskCmd is async-only. Use ExecuteAsync.");

    protected override object? Execute(ICommandContext context, TaskImplementation task)
    {
        throw new NotSupportedException("ResolveTaskCmd is async-only. Use ExecuteAsync.");
    }

    protected override async Task<object?> ExecuteAsync(
        ICommandContext context,
        TaskImplementation task,
        CancellationToken cancellationToken)
    {
        await TaskCommandHelper.UpdateTaskAsync(
            context, TaskId, current => current with { DelegationState = "RESOLVED" }, cancellationToken);

        if (_variables != null && !string.IsNullOrEmpty(task.ProcessInstanceId))
        {
            try
            {
                var execution = await context.FindExecutionByTaskIdAsync(TaskId, cancellationToken);
                if (execution == null)
                    return null;

                foreach (var kvp in _variables)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    execution.SetVariable(kvp.Key, kvp.Value);
                }
            }
            catch (System.ArgumentException)
            {
            }
        }

        return null;
    }

    protected override string GetSuspendedTaskException() => "Cannot resolve a suspended task";
}

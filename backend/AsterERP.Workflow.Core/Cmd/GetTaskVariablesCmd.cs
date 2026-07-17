using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Cmd;

public class GetTaskVariablesCmd : ICommand<Dictionary<string, object?>>
{
    private readonly string _taskId;
    private readonly bool _localScope;

    public GetTaskVariablesCmd(string taskId, bool localScope = false)
    {
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        _localScope = localScope;
    }


    public async Task<Dictionary<string, object?>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        var result = new Dictionary<string, object?>();
        try
        {
            var execution = await context.GetCurrentExecutionAsync(_taskId, cancellationToken);
            if (_localScope)
            {
                foreach (var kvp in execution!.Variables)
                    result[kvp.Key] = kvp.Value;
            }
            else
            {
                var current = execution;
                while (current != null)
                {
                    foreach (var kvp in current.Variables)
                    {
                        if (!result.ContainsKey(kvp.Key))
                            result[kvp.Key] = kvp.Value;
                    }
                    current = current.Parent;
                }
            }
        }
        catch (ArgumentException)
        {
        }
        return result;
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Cmd;

public class GetExecutionVariablesCmd : ICommand<Dictionary<string, object?>>
{
    private readonly string _executionId;
    private readonly bool _localScope;

    public GetExecutionVariablesCmd(string executionId, bool localScope = false)
    {
        _executionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        _localScope = localScope;
    }


    public async Task<Dictionary<string, object?>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");

        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        var result = new Dictionary<string, object?>();
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
        return result;
    }
}

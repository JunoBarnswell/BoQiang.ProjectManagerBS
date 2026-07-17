using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Cmd;

public class FindActiveActivityIdsCmd : ICommand<List<string>>
{
    private readonly string _processInstanceId;

    public FindActiveActivityIdsCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId ?? throw new ArgumentNullException(nameof(processInstanceId));
    }


    public async Task<List<string>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");

        var result = new List<string>();
        try
        {
            var execution = await context.GetCurrentExecutionAsync(_processInstanceId, cancellationToken);
            if (execution != null && execution.IsActive && execution.CurrentActivityId != null)
                result.Add(execution.CurrentActivityId);

            if (execution != null)
            {
                foreach (var child in execution.ChildExecutions)
                {
                    if (child.IsActive && child.CurrentActivityId != null)
                        result.Add(child.CurrentActivityId);
                }
            }
        }
        catch (ArgumentException)
        {
        }

        return result;
    }
}

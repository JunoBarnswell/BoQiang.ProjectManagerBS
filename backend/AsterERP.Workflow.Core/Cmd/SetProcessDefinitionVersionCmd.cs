using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Cmd;

public class SetProcessDefinitionVersionCmd : ICommand<object?>
{
    private readonly string _processDefinitionId;
    private readonly int _newVersion;

    public SetProcessDefinitionVersionCmd(string processDefinitionId, int newVersion)
    {
        _processDefinitionId = processDefinitionId ?? throw new ArgumentNullException(nameof(processDefinitionId));
        _newVersion = newVersion;
    }

    public object? Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_processDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");

        if (_newVersion < 1)
            throw new WorkflowEngineArgumentException("process definition version must be 1 or higher");

        return null;
    }

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

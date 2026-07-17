using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Cmd;

public class DeleteDeploymentCmd : ICommand<object?>
{
    private readonly string _deploymentId;
    private readonly bool _cascade;

    public DeleteDeploymentCmd(string deploymentId, bool cascade = false)
    {
        _deploymentId = deploymentId ?? throw new ArgumentNullException(nameof(deploymentId));
        _cascade = cascade;
    }

    public object? Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_deploymentId))
            throw new WorkflowEngineArgumentException("deploymentId is null");

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.ENTITY_DELETED,
                    new { DeploymentId = _deploymentId, Cascade = _cascade }));
        }

        return null;
    }

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Cmd;

public class GetBpmnModelCmd : ICommand<BpmnModel.BpmnModel>
{
    private readonly string _processDefinitionId;

    public GetBpmnModelCmd(string processDefinitionId)
    {
        _processDefinitionId = processDefinitionId ?? throw new ArgumentNullException(nameof(processDefinitionId));
    }

    public BpmnModel.BpmnModel Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_processDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");

        var deploymentManager = context.ProcessEngineConfiguration.DeploymentManager;
        if (deploymentManager == null)
            return new BpmnModel.BpmnModel();

        var cacheEntry = deploymentManager.ResolveProcessDefinition(_processDefinitionId);
        return cacheEntry?.BpmnModel ?? new BpmnModel.BpmnModel();
    }

    public Task<BpmnModel.BpmnModel> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class GetDeploymentProcessModelCmd : ICommand<byte[]?>
{
    private readonly string _processDefinitionId;

    public GetDeploymentProcessModelCmd(string processDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(processDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");

        _processDefinitionId = processDefinitionId;
    }

    public byte[]? Execute(ICommandContext context)
    {
        var deploymentManager = context.ProcessEngineConfiguration.DeploymentManager;
        if (deploymentManager == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No process definition found for id = '{_processDefinitionId}'",
                typeof(ProcessDefinitionRecord));

        var cacheEntry = deploymentManager.ResolveProcessDefinition(_processDefinitionId);
        if (cacheEntry?.BpmnModel == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No process definition model found for id = '{_processDefinitionId}'",
                typeof(ProcessDefinitionRecord));

        var exporter = new BpmnParser.BpmnModelExporter();
        return System.Text.Encoding.UTF8.GetBytes(exporter.ExportToXml(cacheEntry.BpmnModel));
    }

    public Task<byte[]?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

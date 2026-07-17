using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Workflow.Core.Deployer;

public class BpmnDeploymentHelper
{
    public BpmnDeploymentHelper() { }

    public async Task VerifyProcessDefinitionsDoNotOverlapAsync(
        ParsedDeployment parsedDeployment,
        CancellationToken cancellationToken = default)
    {
        var processKeys = new HashSet<string>();
        foreach (var processDef in parsedDeployment.ProcessDefinitions)
        {
            if (!processKeys.Add(processDef.ProcessDefinition.Key))
            {
                throw new InvalidOperationException(
                    $"Duplicate process definition key '{processDef.ProcessDefinition.Key}' found in deployment");
            }
        }

        await Task.CompletedTask;
    }

    public async Task CopyDeploymentResourcesAsync(
        ParsedDeployment parsedDeployment,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }

    public async Task CreateProcessDefinitionInformationAsync(
        ProcessDefinitionInfo processDefinition,
        BpmnModelType bpmnModel,
        CancellationToken cancellationToken = default)
    {
        var process = bpmnModel.Processes.FirstOrDefault();
        if (process == null) return;

        processDefinition.Name = process.Name ?? process.Id;

        await Task.CompletedTask;
    }

    public async Task UpdateProcessDefinitionCacheAsync(
        ProcessDefinitionInfo processDefinition,
        BpmnModelType bpmnModel,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }
}

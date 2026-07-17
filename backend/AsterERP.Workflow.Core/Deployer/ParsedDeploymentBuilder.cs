using System.Collections.Generic;
using AsterERP.Workflow.Core.Expression;
using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Workflow.Core.Deployer;

public class ParsedDeploymentBuilder
{
    private readonly string _deploymentId;
    private readonly IExpressionManager? _expressionManager;
    private readonly List<(string ResourceName, BpmnModelType Model)> _bpmnModels = new();

    public ParsedDeploymentBuilder(string deploymentId, IExpressionManager? expressionManager = null)
    {
        _deploymentId = deploymentId;
        _expressionManager = expressionManager;
    }

    public ParsedDeploymentBuilder AddBpmnModel(string resourceName, BpmnModelType bpmnModel)
    {
        _bpmnModels.Add((resourceName, bpmnModel));
        return this;
    }

    public ParsedDeployment Build()
    {
        var parsedDeployment = new ParsedDeployment(_deploymentId);

        foreach (var (resourceName, bpmnModel) in _bpmnModels)
        {
            if (bpmnModel.Processes == null) continue;

            foreach (var process in bpmnModel.Processes)
            {
                var processDefinition = CreateProcessDefinition(process, resourceName);
                parsedDeployment.AddProcessDefinition(processDefinition, bpmnModel, resourceName);
            }
        }

        return parsedDeployment;
    }

    private ProcessDefinitionInfo CreateProcessDefinition(AsterERP.Workflow.BpmnModel.Process process, string resourceName)
    {
        var processId = process.Id ?? string.Empty;

        return new ProcessDefinitionInfo
        {
            Id = $"{processId}:{_deploymentId}",
            Key = processId,
            Name = process.Name ?? processId,
            DeploymentId = _deploymentId,
            ResourceName = resourceName,
            Version = 1,
            IsSuspended = false,
            BpmnModel = null
        };
    }
}

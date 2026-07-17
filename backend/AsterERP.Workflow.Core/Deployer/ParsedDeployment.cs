using System.Collections.Generic;
using System.Linq;
using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Workflow.Core.Deployer;

public class ParsedDeployment
{
    public string DeploymentId { get; }
    public List<ParsedProcessDefinition> ProcessDefinitions { get; } = new();
    private readonly Dictionary<string, BpmnModelType> _bpmnModelMap = new();
    private readonly Dictionary<string, string> _resourceNameMap = new();

    public ParsedDeployment(string deploymentId)
    {
        DeploymentId = deploymentId;
    }

    public void AddProcessDefinition(ProcessDefinitionInfo processDefinition, BpmnModelType bpmnModel, string resourceName)
    {
        ProcessDefinitions.Add(new ParsedProcessDefinition(processDefinition, bpmnModel, resourceName));
        _bpmnModelMap[processDefinition.Id] = bpmnModel;
        _resourceNameMap[processDefinition.Id] = resourceName;
    }

    public BpmnModelType? GetBpmnModelForProcess(ProcessDefinitionInfo processDefinition)
    {
        return _bpmnModelMap.GetValueOrDefault(processDefinition.Id);
    }

    public string? GetResourceNameForProcess(ProcessDefinitionInfo processDefinition)
    {
        return _resourceNameMap.GetValueOrDefault(processDefinition.Id);
    }

    public AsterERP.Workflow.BpmnModel.Process? GetProcessForProcessDefinition(ProcessDefinitionInfo processDefinition)
    {
        var bpmnModel = GetBpmnModelForProcess(processDefinition);
        if (bpmnModel == null) return null;

        return bpmnModel.Processes.FirstOrDefault(p => p.Id == processDefinition.Key);
    }

    public void UpdateProcessDefinitionId(string oldId, string newId)
    {
        var pd = ProcessDefinitions.FirstOrDefault(p => p.ProcessDefinition.Id == oldId);
        if (pd != null)
        {
            pd.ProcessDefinition.Id = newId;
            if (_bpmnModelMap.Remove(oldId, out var model))
            {
                _bpmnModelMap[newId] = model;
            }
            if (_resourceNameMap.Remove(oldId, out var resName))
            {
                _resourceNameMap[newId] = resName;
            }
        }
    }
}

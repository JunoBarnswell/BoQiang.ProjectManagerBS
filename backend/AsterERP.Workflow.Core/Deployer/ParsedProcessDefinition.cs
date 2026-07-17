using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Workflow.Core.Deployer;

public class ParsedProcessDefinition
{
    public ProcessDefinitionInfo ProcessDefinition { get; }
    public BpmnModelType BpmnModel { get; }
    public string ResourceName { get; }

    public ParsedProcessDefinition(
        ProcessDefinitionInfo processDefinition,
        BpmnModelType bpmnModel,
        string resourceName)
    {
        ProcessDefinition = processDefinition;
        BpmnModel = bpmnModel;
        ResourceName = resourceName;
    }
}

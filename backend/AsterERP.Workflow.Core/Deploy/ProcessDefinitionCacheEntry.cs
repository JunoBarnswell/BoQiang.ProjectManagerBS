using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Workflow.Core.Deploy;

public class ProcessDefinitionCacheEntry
{
    public Deployer.ProcessDefinitionInfo ProcessDefinition { get; set; }
    public BpmnModelType BpmnModel { get; set; }
    public AsterERP.Workflow.BpmnModel.Process? Process { get; set; }

    public ProcessDefinitionCacheEntry(
        Deployer.ProcessDefinitionInfo processDefinition,
        BpmnModelType bpmnModel,
        AsterERP.Workflow.BpmnModel.Process? process)
    {
        ProcessDefinition = processDefinition;
        BpmnModel = bpmnModel;
        Process = process;
    }
}

namespace AsterERP.Workflow.BpmnModel.Image;

public interface IPngProcessDiagramGenerator
{
    byte[] GeneratePng(BpmnModel bpmnModel, string? processId = null);
}

namespace AsterERP.Workflow.BpmnModel.Image;

public interface ISvgProcessDiagramGenerator
{
    string GenerateSvg(BpmnModel bpmnModel, string? processId = null);
    string GenerateSvg(BpmnModel bpmnModel, Dictionary<string, GraphicInfo> graphicInfo);
    byte[] GenerateDiagram(BpmnModel bpmnModel, string? processId = null);
}

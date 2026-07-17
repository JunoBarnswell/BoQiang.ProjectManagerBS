using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class ExclusiveGatewayParseHandler : AbstractFlowNodeBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "exclusiveGateway" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        return new ExclusiveGateway
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            DefaultFlow = GetAttributeValue(xmlNode, "default")
        };
    }
}

using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class EventBasedGatewayParseHandler : AbstractFlowNodeBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "eventBasedGateway" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var gatewayType = GetAttributeValue(xmlNode, "eventGatewayType") ?? "Exclusive";
        var instantiate = GetAttributeValue(xmlNode, "instantiate") == "true";

        return new EventGateway
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            EventGatewayType = gatewayType switch
            {
                "Parallel" => EventGatewayType.Parallel,
                "EventBased" => EventGatewayType.EventBased,
                _ => EventGatewayType.Exclusive
            },
            Instantiate = instantiate
        };
    }
}

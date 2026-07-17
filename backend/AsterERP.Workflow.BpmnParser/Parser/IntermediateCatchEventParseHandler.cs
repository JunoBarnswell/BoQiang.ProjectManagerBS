using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class IntermediateCatchEventParseHandler : AbstractFlowNodeBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "intermediateCatchEvent" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var catchEvent = new IntermediateCatchEvent
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name")
        };

        ParseEventDefinitions(xmlNode, catchEvent);

        return catchEvent;
    }

    private void ParseEventDefinitions(XmlNode xmlNode, CatchEvent catchEvent)
    {
        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            var eventDefinition = EventDefinitionParserHelper.ParseEventDefinition(child);
            if (eventDefinition != null)
                catchEvent.EventDefinitions.Add(eventDefinition);
        }
    }
}

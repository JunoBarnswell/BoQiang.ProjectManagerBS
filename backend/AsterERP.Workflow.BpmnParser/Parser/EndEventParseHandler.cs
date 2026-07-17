using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class EndEventParseHandler : AbstractFlowNodeBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "endEvent" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var endEvent = new EndEvent
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name")
        };

        ParseEventDefinitions(xmlNode, endEvent);

        return endEvent;
    }

    private void ParseEventDefinitions(XmlNode xmlNode, ThrowEvent throwEvent)
    {
        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            var eventDefinition = EventDefinitionParserHelper.ParseEventDefinition(child);
            if (eventDefinition != null)
                throwEvent.EventDefinitions.Add(eventDefinition);
        }
    }
}

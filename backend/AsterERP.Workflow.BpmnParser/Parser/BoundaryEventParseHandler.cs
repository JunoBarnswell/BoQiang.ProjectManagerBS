using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class BoundaryEventParseHandler : AbstractFlowNodeBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "boundaryEvent" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var boundary = new BoundaryEvent
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            CancelActivity = GetAttributeValue(xmlNode, "cancelActivity") != "false",
            AttachedToRefId = GetAttributeValue(xmlNode, "attachedToRef")
        };

        ParseEventDefinitions(xmlNode, boundary);

        return boundary;
    }

    protected override void PostProcessElement(XmlNode xmlNode, FlowElement element, BpmnModel.BpmnModel model, Process activeProcess)
    {
        if (element is BoundaryEvent boundary && boundary.AttachedToRefId != null)
        {
            var attachedTo = activeProcess.FlowElements
                .FirstOrDefault(fe => fe.Id == boundary.AttachedToRefId);
            if (attachedTo is Activity activity)
            {
                boundary.AttachedToRef = attachedTo;
                activity.BoundaryEvents.Add(boundary);
            }
        }
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

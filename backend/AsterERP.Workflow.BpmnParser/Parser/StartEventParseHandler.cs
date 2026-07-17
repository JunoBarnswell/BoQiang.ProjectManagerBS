using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class StartEventParseHandler : AbstractFlowNodeBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "startEvent" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var startEvent = new StartEvent
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            IsInterrupting = GetAttributeValue(xmlNode, "isInterrupting") != "false",
            Initiator = GetAttributeValue(xmlNode, "activiti", "initiator"),
            FormKey = GetAttributeValue(xmlNode, "activiti", "formKey")
        };

        ParseEventDefinitions(xmlNode, startEvent);
        ParseFormProperties(xmlNode, startEvent);

        return startEvent;
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

    private void ParseFormProperties(XmlNode xmlNode, StartEvent startEvent)
    {
        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "extensionElements")
            {
                foreach (XmlNode extChild in child.ChildNodes)
                {
                    if (extChild.NodeType != XmlNodeType.Element) continue;
                    if (extChild.LocalName == "formProperty")
                    {
                        var formProp = new FormProperty
                        {
                            Id = GetAttributeValue(extChild, "id"),
                            Name = GetAttributeValue(extChild, "name"),
                            Type = GetAttributeValue(extChild, "type"),
                            Variable = GetAttributeValue(extChild, "variable"),
                            Expression = GetAttributeValue(extChild, "expression"),
                            DefaultExpression = GetAttributeValue(extChild, "default")
                        };
                        var requiredStr = GetAttributeValue(extChild, "required");
                        if (requiredStr != null) formProp.Required = requiredStr == "true";
                        var readableStr = GetAttributeValue(extChild, "readable");
                        if (readableStr != null) formProp.Readable = readableStr == "true";
                        var writableStr = GetAttributeValue(extChild, "writable");
                        if (writableStr != null) formProp.Writable = writableStr == "true";
                        startEvent.FormProperties.Add(formProp);
                    }
                }
            }
        }
    }
}

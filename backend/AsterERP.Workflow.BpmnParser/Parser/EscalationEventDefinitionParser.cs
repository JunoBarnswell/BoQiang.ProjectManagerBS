using System.Xml;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class EscalationEventDefinitionParser : BaseChildElementParser
{
    public override string ElementName => "escalationEventDefinition";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var escalationDef = new BpmnModelNs.EscalationEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            EscalationRef = GetAttributeValue(xmlNode, "escalationRef"),
            EscalationCode = GetAttributeValue(xmlNode, "escalationCode")
        };

        AddEventDefinition(parentElement, escalationDef);
    }

    private static void AddEventDefinition(BpmnModelNs.BaseElement parentElement, BpmnModelNs.EventDefinition def)
    {
        if (parentElement is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (parentElement is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }
}

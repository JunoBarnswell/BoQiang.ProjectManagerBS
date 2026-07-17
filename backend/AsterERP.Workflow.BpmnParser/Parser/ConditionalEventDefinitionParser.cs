using System.Xml;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class ConditionalEventDefinitionParser : BaseChildElementParser
{
    public override string ElementName => "conditionalEventDefinition";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var conditionalDef = new BpmnModelNs.ConditionalEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element)
                continue;

            if (child.LocalName == "condition" || child.LocalName == "conditionExpression")
            {
                var value = child.InnerText;
                conditionalDef.Condition = value;
                conditionalDef.ConditionExpression = value;
                break;
            }
        }

        AddEventDefinition(parentElement, conditionalDef);
    }

    private static void AddEventDefinition(BpmnModelNs.BaseElement parentElement, BpmnModelNs.EventDefinition def)
    {
        if (parentElement is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (parentElement is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }
}

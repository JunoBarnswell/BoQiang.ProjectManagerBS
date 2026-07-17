using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class ConditionalEventDefinitionXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.ConditionalEventDefinition);
    public override string GetXMLElementName() => "conditionalEventDefinition";

    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var conditionalDef = new BpmnModelNs.ConditionalEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element)
                continue;

            if (child.LocalName is "condition" or "conditionExpression")
            {
                var value = child.InnerText;
                conditionalDef.Condition = value;
                conditionalDef.ConditionExpression = value;
                break;
            }
        }

        return conditionalDef;
    }

    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }

    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var conditionalDef = (BpmnModelNs.ConditionalEventDefinition)element;
        var value = conditionalDef.ConditionExpression ?? conditionalDef.Condition;
        if (!string.IsNullOrWhiteSpace(value))
        {
            xtw.WriteStartElement("condition");
            xtw.WriteString(value);
            xtw.WriteEndElement();
        }
    }
}

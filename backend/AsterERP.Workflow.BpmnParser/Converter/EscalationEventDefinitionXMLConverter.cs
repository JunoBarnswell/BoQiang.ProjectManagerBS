using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class EscalationEventDefinitionXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.EscalationEventDefinition);
    public override string GetXMLElementName() => "escalationEventDefinition";

    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        return new BpmnModelNs.EscalationEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            EscalationRef = GetAttributeValue(xmlNode, "escalationRef"),
            EscalationCode = GetAttributeValue(xmlNode, "escalationCode")
        };
    }

    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var escalationDef = (BpmnModelNs.EscalationEventDefinition)element;
        BpmnXMLUtil.WriteDefaultAttribute("escalationRef", escalationDef.EscalationRef, xtw);
        BpmnXMLUtil.WriteDefaultAttribute("escalationCode", escalationDef.EscalationCode, xtw);
    }

    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


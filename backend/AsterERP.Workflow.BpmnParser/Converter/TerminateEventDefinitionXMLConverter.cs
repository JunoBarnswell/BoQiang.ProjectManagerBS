using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class TerminateEventDefinitionXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.TerminateEventDefinition);
    public override string GetXMLElementName() => "terminateEventDefinition";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        return new BpmnModelNs.TerminateEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            TerminateAll = GetAttributeValue(xmlNode, "terminateAll") == "true"
        };
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var terminateDef = (BpmnModelNs.TerminateEventDefinition)element;
        if (terminateDef.TerminateAll)
            BpmnXMLUtil.WriteDefaultAttribute("terminateAll", "true", xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


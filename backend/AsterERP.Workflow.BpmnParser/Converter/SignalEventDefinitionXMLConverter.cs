using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class SignalEventDefinitionXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.SignalEventDefinition);
    public override string GetXMLElementName() => "signalEventDefinition";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        return new BpmnModelNs.SignalEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            SignalRef = GetAttributeValue(xmlNode, "signalRef"),
            Scope = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "scope")
        };
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var signalDef = (BpmnModelNs.SignalEventDefinition)element;
        BpmnXMLUtil.WriteDefaultAttribute("signalRef", signalDef.SignalRef, xtw);
        BpmnXMLUtil.WriteQualifiedAttribute("scope", signalDef.Scope, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class StartEventXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.StartEvent);
    public override string GetXMLElementName() => "startEvent";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var startEvent = new BpmnModelNs.StartEvent
        {
            IsInterrupting = GetAttributeValue(xmlNode, "isInterrupting")?.ToLowerInvariant() != "false",
            Initiator = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "initiator"),
            FormKey = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "formKey")
        };
        ParseChildElements("startEvent", startEvent, xmlNode, model);
        return startEvent;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var startEvent = (BpmnModelNs.StartEvent)element;
        if (!startEvent.IsInterrupting) BpmnXMLUtil.WriteDefaultAttribute("isInterrupting", "false", xtw);
        if (!string.IsNullOrEmpty(startEvent.Initiator)) BpmnXMLUtil.WriteQualifiedAttribute("initiator", startEvent.Initiator, xtw);
        if (!string.IsNullOrEmpty(startEvent.FormKey)) BpmnXMLUtil.WriteQualifiedAttribute("formKey", startEvent.FormKey, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var startEvent = (BpmnModelNs.StartEvent)element;
        if (startEvent.EventDefinitions.Count > 0) WriteEventDefinitions(startEvent, startEvent.EventDefinitions, model, xtw);
    }
}


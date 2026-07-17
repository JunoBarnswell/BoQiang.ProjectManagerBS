using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class BoundaryEventXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.BoundaryEvent);
    public override string GetXMLElementName() => "boundaryEvent";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var boundary = new BpmnModelNs.BoundaryEvent
        {
            CancelActivity = GetAttributeValue(xmlNode, "cancelActivity")?.ToLowerInvariant() != "false",
            AttachedToRefId = GetAttributeValue(xmlNode, "attachedToRef")
        };
        ParseChildElements("boundaryEvent", boundary, xmlNode, model);
        return boundary;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var boundary = (BpmnModelNs.BoundaryEvent)element;
        if (!boundary.CancelActivity) BpmnXMLUtil.WriteDefaultAttribute("cancelActivity", "false", xtw);
        BpmnXMLUtil.WriteDefaultAttribute("attachedToRef", boundary.AttachedToRefId, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var boundary = (BpmnModelNs.BoundaryEvent)element;
        if (boundary.EventDefinitions.Count > 0) WriteEventDefinitions(boundary, boundary.EventDefinitions, model, xtw);
    }
}


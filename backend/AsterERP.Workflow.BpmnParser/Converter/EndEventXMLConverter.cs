using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class EndEventXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.EndEvent);
    public override string GetXMLElementName() => "endEvent";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var endEvent = new BpmnModelNs.EndEvent();
        ParseChildElements("endEvent", endEvent, xmlNode, model);
        return endEvent;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var endEvent = (BpmnModelNs.EndEvent)element;
        if (endEvent.EventDefinitions.Count > 0) WriteEventDefinitions(endEvent, endEvent.EventDefinitions, model, xtw);
    }
}


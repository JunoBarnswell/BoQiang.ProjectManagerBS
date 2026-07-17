using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class CatchEventXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.IntermediateCatchEvent);
    public override string GetXMLElementName() => "intermediateCatchEvent";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var catchEvent = new BpmnModelNs.IntermediateCatchEvent();
        ParseChildElements("intermediateCatchEvent", catchEvent, xmlNode, model);
        return catchEvent;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var catchEvent = (BpmnModelNs.IntermediateCatchEvent)element;
        if (catchEvent.EventDefinitions.Count > 0) WriteEventDefinitions(catchEvent, catchEvent.EventDefinitions, model, xtw);
    }
}


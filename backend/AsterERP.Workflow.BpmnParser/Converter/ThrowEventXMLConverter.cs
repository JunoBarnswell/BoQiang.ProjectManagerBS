using System;
using System.Xml;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class ThrowEventXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.IntermediateThrowEvent);
    public override string GetXMLElementName() => "intermediateThrowEvent";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var throwEvent = new BpmnModelNs.IntermediateThrowEvent();
        ParseChildElements("intermediateThrowEvent", throwEvent, xmlNode, model);
        return throwEvent;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var throwEvent = (BpmnModelNs.IntermediateThrowEvent)element;
        if (throwEvent.EventDefinitions.Count > 0)
            WriteEventDefinitions(throwEvent, throwEvent.EventDefinitions, model, xtw);
    }
}

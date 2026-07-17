using System;
using System.Xml;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class InclusiveGatewayXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.InclusiveGateway);
    public override string GetXMLElementName() => "inclusiveGateway";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model) =>
        new BpmnModelNs.InclusiveGateway { DefaultFlow = GetAttributeValue(xmlNode, "default") };
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var gateway = (BpmnModelNs.InclusiveGateway)element;
        BpmnXMLUtil.WriteDefaultAttribute("default", gateway.DefaultFlow, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}

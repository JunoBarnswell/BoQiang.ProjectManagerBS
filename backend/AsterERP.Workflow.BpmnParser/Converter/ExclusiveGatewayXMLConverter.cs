using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class ExclusiveGatewayXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.ExclusiveGateway);
    public override string GetXMLElementName() => "exclusiveGateway";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model) => new BpmnModelNs.ExclusiveGateway { DefaultFlow = GetAttributeValue(xmlNode, "default") };
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var gateway = (BpmnModelNs.ExclusiveGateway)element;
        BpmnXMLUtil.WriteDefaultAttribute("default", gateway.DefaultFlow, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


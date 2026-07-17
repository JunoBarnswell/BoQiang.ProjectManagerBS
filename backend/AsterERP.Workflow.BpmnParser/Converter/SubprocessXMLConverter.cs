using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class SubprocessXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.SubProcess);
    public override string GetXMLElementName() => "subProcess";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var sp = new BpmnModelNs.SubProcess { TriggeredByEvent = GetAttributeValue(xmlNode, "triggeredByEvent")?.ToLowerInvariant() == "true" };
        ParseChildElements("subProcess", sp, xmlNode, model);
        return sp;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var sp = (BpmnModelNs.SubProcess)element;
        if (sp.TriggeredByEvent) BpmnXMLUtil.WriteDefaultAttribute("triggeredByEvent", "true", xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


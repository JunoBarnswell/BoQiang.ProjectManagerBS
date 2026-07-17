using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class DataStoreReferenceXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.DataStoreReference);
    public override string GetXMLElementName() => "dataStoreReference";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model) => new BpmnModelNs.DataStoreReference
    {
        DataStoreRef = GetAttributeValue(xmlNode, "dataStoreRef"),
        ItemSubjectRef = GetAttributeValue(xmlNode, "itemSubjectRef")
    };
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var dsRef = (BpmnModelNs.DataStoreReference)element;
        BpmnXMLUtil.WriteDefaultAttribute("dataStoreRef", dsRef.DataStoreRef, xtw);
        BpmnXMLUtil.WriteDefaultAttribute("itemSubjectRef", dsRef.ItemSubjectRef, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


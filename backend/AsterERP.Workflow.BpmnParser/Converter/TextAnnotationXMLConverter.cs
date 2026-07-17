using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class TextAnnotationXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.TextAnnotation);
    public override string GetXMLElementName() => "textAnnotation";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var textAnnotation = new BpmnModelNs.TextAnnotation();
        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "text") textAnnotation.Text = child.InnerText;
        }
        return textAnnotation;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var textAnnotation = (BpmnModelNs.TextAnnotation)element;
        if (!string.IsNullOrEmpty(textAnnotation.Text))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "text", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteString(textAnnotation.Text);
            xtw.WriteEndElement();
        }
    }
}


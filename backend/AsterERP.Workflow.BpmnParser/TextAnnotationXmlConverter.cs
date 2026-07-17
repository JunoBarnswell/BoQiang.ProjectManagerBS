using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class TextAnnotationXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "textAnnotation" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var textAnnotation = new BpmnModelNs.TextAnnotation
        {
            Id = GetAttributeValue(node, "id")
        };

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element)
                continue;
            if (child.LocalName == "text")
                textAnnotation.Text = child.InnerText;
        }

        return textAnnotation;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var textAnnotation = (BpmnModelNs.TextAnnotation)element;
        var el = CreateBpmnElement(document, "textAnnotation");
        SetAttribute(el, "id", textAnnotation.Id);

        if (!string.IsNullOrWhiteSpace(textAnnotation.Text))
        {
            var textElement = CreateBpmnElement(document, "text");
            textElement.InnerText = textAnnotation.Text;
            el.AppendChild(textElement);
        }

        parentElement.AppendChild(el);
    }
}


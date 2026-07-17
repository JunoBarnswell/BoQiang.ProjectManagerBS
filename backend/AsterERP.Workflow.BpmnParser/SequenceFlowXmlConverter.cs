using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class SequenceFlowXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "sequenceFlow" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var sf = new BpmnModelNs.SequenceFlow
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            SourceRef = GetAttributeValue(node, "sourceRef"),
            TargetRef = GetAttributeValue(node, "targetRef")
        };
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.LocalName == "conditionExpression" && child.NodeType == XmlNodeType.Element)
                sf.ConditionExpression = child.InnerText;
        }
        return sf;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var sf = (BpmnModelNs.SequenceFlow)element;
        var el = CreateBpmnElement(document, "sequenceFlow");
        SetAttribute(el, "id", sf.Id);
        SetAttribute(el, "name", sf.Name);
        SetAttribute(el, "sourceRef", sf.SourceRef);
        SetAttribute(el, "targetRef", sf.TargetRef);
        if (sf.ConditionExpression != null)
        {
            var condEl = CreateBpmnElement(document, "conditionExpression");
            condEl.InnerText = sf.ConditionExpression;
            el.AppendChild(condEl);
        }
        parentElement.AppendChild(el);
    }
}


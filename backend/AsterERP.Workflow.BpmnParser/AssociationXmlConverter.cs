using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class AssociationXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "association" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return new BpmnModelNs.Association
        {
            Id = GetAttributeValue(node, "id"),
            SourceRef = GetAttributeValue(node, "sourceRef"),
            TargetRef = GetAttributeValue(node, "targetRef"),
            AssociationDirection = GetAttributeValue(node, "associationDirection")
        };
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var association = (BpmnModelNs.Association)element;
        var el = CreateBpmnElement(document, "association");
        SetAttribute(el, "id", association.Id);
        SetAttribute(el, "sourceRef", association.SourceRef);
        SetAttribute(el, "targetRef", association.TargetRef);
        SetAttribute(el, "associationDirection", association.AssociationDirection);
        parentElement.AppendChild(el);
    }
}


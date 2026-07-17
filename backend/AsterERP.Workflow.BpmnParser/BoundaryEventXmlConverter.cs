using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class BoundaryEventXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "boundaryEvent" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var boundary = new BpmnModelNs.BoundaryEvent
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            CancelActivity = !string.Equals(GetAttributeValue(node, "cancelActivity"), "false", StringComparison.OrdinalIgnoreCase),
            AttachedToRefId = GetAttributeValue(node, "attachedToRef")
        };
        ParseEventDefinitions(node, boundary);
        if (boundary.EventDefinitions.Count == 1 && boundary.EventDefinitions[0] is BpmnModelNs.ErrorEventDefinition)
            boundary.CancelActivity = false;
        return boundary;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var boundary = (BpmnModelNs.BoundaryEvent)element;
        var el = CreateBpmnElement(document, "boundaryEvent");
        SetAttribute(el, "id", boundary.Id);
        SetAttribute(el, "name", boundary.Name);
        WriteEventDefinitions(boundary, el, document);
        parentElement.AppendChild(el);
    }
}


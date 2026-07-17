using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class IntermediateThrowEventXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "intermediateThrowEvent" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var throwEvent = new BpmnModelNs.IntermediateThrowEvent
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
        };
        ParseEventDefinitions(node, throwEvent);
        return throwEvent;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var throwEvent = (BpmnModelNs.IntermediateThrowEvent)element;
        var el = CreateBpmnElement(document, "intermediateThrowEvent");
        SetAttribute(el, "id", throwEvent.Id);
        SetAttribute(el, "name", throwEvent.Name);
        WriteEventDefinitions(throwEvent, el, document);
        parentElement.AppendChild(el);
    }
}


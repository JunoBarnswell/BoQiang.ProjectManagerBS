using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class EndEventXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "endEvent" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var endEvent = new BpmnModelNs.EndEvent
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
        };
        ParseEventDefinitions(node, endEvent);
        return endEvent;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var endEvent = (BpmnModelNs.EndEvent)element;
        var el = CreateBpmnElement(document, "endEvent");
        SetAttribute(el, "id", endEvent.Id);
        SetAttribute(el, "name", endEvent.Name);
        WriteEventDefinitions(endEvent, el, document);
        parentElement.AppendChild(el);
    }
}


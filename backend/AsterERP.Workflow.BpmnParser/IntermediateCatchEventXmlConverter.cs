using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class IntermediateCatchEventXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "intermediateCatchEvent" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var catchEvent = new BpmnModelNs.IntermediateCatchEvent
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
        };
        ParseEventDefinitions(node, catchEvent);
        return catchEvent;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var catchEvent = (BpmnModelNs.IntermediateCatchEvent)element;
        var el = CreateBpmnElement(document, "intermediateCatchEvent");
        SetAttribute(el, "id", catchEvent.Id);
        SetAttribute(el, "name", catchEvent.Name);
        WriteEventDefinitions(catchEvent, el, document);
        parentElement.AppendChild(el);
    }
}


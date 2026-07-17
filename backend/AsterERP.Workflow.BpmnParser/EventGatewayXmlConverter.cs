using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class EventGatewayXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "eventBasedGateway", "eventGateway" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return new BpmnModelNs.EventGateway
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
        };
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var eventGateway = (BpmnModelNs.EventGateway)element;
        var el = CreateBpmnElement(document, "eventBasedGateway");
        SetAttribute(el, "id", eventGateway.Id);
        SetAttribute(el, "name", eventGateway.Name);
        parentElement.AppendChild(el);
    }
}


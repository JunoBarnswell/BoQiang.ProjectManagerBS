using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class ParallelGatewayXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "parallelGateway" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return new BpmnModelNs.ParallelGateway
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
        };
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var gateway = (BpmnModelNs.ParallelGateway)element;
        var el = CreateBpmnElement(document, "parallelGateway");
        SetAttribute(el, "id", gateway.Id);
        SetAttribute(el, "name", gateway.Name);
        parentElement.AppendChild(el);
    }
}


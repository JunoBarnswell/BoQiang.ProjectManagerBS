using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class ComplexGatewayXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "complexGateway" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return new BpmnModelNs.ComplexGateway
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            DefaultFlow = GetAttributeValue(node, "default")
        };
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var gateway = (BpmnModelNs.ComplexGateway)element;
        var el = CreateBpmnElement(document, "complexGateway");
        SetAttribute(el, "id", gateway.Id);
        SetAttribute(el, "name", gateway.Name);
        SetAttribute(el, "default", gateway.DefaultFlow);
        parentElement.AppendChild(el);
    }
}


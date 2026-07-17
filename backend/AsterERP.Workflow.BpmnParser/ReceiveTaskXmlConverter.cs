using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class ReceiveTaskXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "receiveTask" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return new BpmnModelNs.ReceiveTask
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
        };
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var receiveTask = (BpmnModelNs.ReceiveTask)element;
        var el = CreateBpmnElement(document, "receiveTask");
        SetAttribute(el, "id", receiveTask.Id);
        SetAttribute(el, "name", receiveTask.Name);
        parentElement.AppendChild(el);
    }
}


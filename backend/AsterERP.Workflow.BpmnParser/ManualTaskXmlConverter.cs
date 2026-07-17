using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class ManualTaskXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "manualTask" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return new BpmnModelNs.ManualTask
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
        };
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var manualTask = (BpmnModelNs.ManualTask)element;
        var el = CreateBpmnElement(document, "manualTask");
        SetAttribute(el, "id", manualTask.Id);
        SetAttribute(el, "name", manualTask.Name);
        parentElement.AppendChild(el);
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class BusinessRuleTaskXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "businessRuleTask" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return new BpmnModelNs.BusinessRuleTask
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
        };
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var brTask = (BpmnModelNs.BusinessRuleTask)element;
        var el = CreateBpmnElement(document, "businessRuleTask");
        SetAttribute(el, "id", brTask.Id);
        SetAttribute(el, "name", brTask.Name);
        parentElement.AppendChild(el);
    }
}


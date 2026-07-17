using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class StartEventXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "startEvent" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var startEvent = new BpmnModelNs.StartEvent
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            IsInterrupting = bool.Parse(GetAttributeValue(node, "isInterrupting") ?? "true"),
            Initiator = GetAttributeValue(node, "activiti", "initiator"),
            FormKey = GetAttributeValue(node, "activiti", "formKey")
        };
        ParseEventDefinitions(node, startEvent);
        return startEvent;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var startEvent = (BpmnModelNs.StartEvent)element;
        var el = CreateBpmnElement(document, "startEvent");
        SetAttribute(el, "id", startEvent.Id);
        SetAttribute(el, "name", startEvent.Name);
        if (!startEvent.IsInterrupting)
            el.SetAttribute("isInterrupting", "false");
        if (startEvent.Initiator != null)
            SetAttribute(el, "activiti", "initiator", startEvent.Initiator, BpmnConstants.WorkflowExtensionNamespace);
        if (startEvent.FormKey != null)
            SetAttribute(el, "activiti", "formKey", startEvent.FormKey, BpmnConstants.WorkflowExtensionNamespace);
        WriteEventDefinitions(startEvent, el, document);
        parentElement.AppendChild(el);
    }
}


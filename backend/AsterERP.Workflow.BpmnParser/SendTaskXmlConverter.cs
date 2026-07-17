using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class SendTaskXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "sendTask" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return new BpmnModelNs.SendTask
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            Implementation = GetAttributeValue(node, "implementation"),
            ImplementationType = GetAttributeValue(node, "activiti", "type"),
            OperationRef = GetAttributeValue(node, "operationRef")
        };
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var sendTask = (BpmnModelNs.SendTask)element;
        var el = CreateBpmnElement(document, "sendTask");
        SetAttribute(el, "id", sendTask.Id);
        SetAttribute(el, "name", sendTask.Name);
        if (sendTask.Implementation != null)
            SetAttribute(el, "implementation", sendTask.Implementation);
        if (sendTask.ImplementationType != null)
            SetAttribute(el, "activiti", "type", sendTask.ImplementationType, BpmnConstants.WorkflowExtensionNamespace);
        if (sendTask.OperationRef != null)
            SetAttribute(el, "operationRef", sendTask.OperationRef);
        parentElement.AppendChild(el);
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class ServiceTaskXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "serviceTask" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var implementationAttr = GetAttributeValue(node, "implementation");
        var workflowExtensionClass = GetAttributeValue(node, "activiti", "class");
        var delegateExpression = GetAttributeValue(node, "activiti", "delegateExpression");
        var expression = GetAttributeValue(node, "activiti", "expression");

        return new BpmnModelNs.ServiceTask
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            ImplementationType = GetAttributeValue(node, "activiti", "type"),
            Implementation = implementationAttr ?? workflowExtensionClass ?? delegateExpression ?? expression,
            DelegateExpression = delegateExpression,
            Expression = expression,
            Class = workflowExtensionClass,
            ResultVariableName = GetAttributeValue(node, "activiti", "resultVariableName"),
            ExtensionId = GetAttributeValue(node, "activiti", "connectorId")
        };
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var serviceTask = (BpmnModelNs.ServiceTask)element;
        var el = CreateBpmnElement(document, "serviceTask");
        SetAttribute(el, "id", serviceTask.Id);
        SetAttribute(el, "name", serviceTask.Name);
        if (serviceTask.ImplementationType != null)
            SetAttribute(el, "activiti", "type", serviceTask.ImplementationType, BpmnConstants.WorkflowExtensionNamespace);
        if (serviceTask.Class != null)
            SetAttribute(el, "activiti", "class", serviceTask.Class, BpmnConstants.WorkflowExtensionNamespace);
        if (serviceTask.DelegateExpression != null)
            SetAttribute(el, "activiti", "delegateExpression", serviceTask.DelegateExpression, BpmnConstants.WorkflowExtensionNamespace);
        if (serviceTask.Expression != null)
            SetAttribute(el, "activiti", "expression", serviceTask.Expression, BpmnConstants.WorkflowExtensionNamespace);
        if (serviceTask.ResultVariableName != null)
            SetAttribute(el, "activiti", "resultVariableName", serviceTask.ResultVariableName, BpmnConstants.WorkflowExtensionNamespace);
        if (serviceTask.Implementation != null
            && serviceTask.Class == null
            && serviceTask.DelegateExpression == null
            && serviceTask.Expression == null)
        {
            SetAttribute(el, "implementation", serviceTask.Implementation);
        }
        parentElement.AppendChild(el);
    }
}


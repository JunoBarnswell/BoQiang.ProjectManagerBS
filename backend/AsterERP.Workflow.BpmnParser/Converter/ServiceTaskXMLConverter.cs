using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class ServiceTaskXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.ServiceTask);
    public override string GetXMLElementName() => "serviceTask";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var serviceTask = new BpmnModelNs.ServiceTask
        {
            ImplementationType = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "type"),
            Class = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "class"),
            DelegateExpression = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "delegateExpression"),
            Expression = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "expression"),
            ResultVariableName = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "resultVariableName"),
            ExtensionId = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "connectorId"),
            SkippedExpression = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "skipExpression")
        };
        var implementation = GetAttributeValue(xmlNode, "implementation");
        if (!string.IsNullOrEmpty(implementation) && string.IsNullOrEmpty(serviceTask.Class) && string.IsNullOrEmpty(serviceTask.DelegateExpression) && string.IsNullOrEmpty(serviceTask.Expression))
            serviceTask.Implementation = implementation;
        else
            serviceTask.Implementation = serviceTask.Class ?? serviceTask.DelegateExpression ?? serviceTask.Expression;
        ParseChildElements("serviceTask", serviceTask, xmlNode, model);
        return serviceTask;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var serviceTask = (BpmnModelNs.ServiceTask)element;
        if (!string.IsNullOrEmpty(serviceTask.ImplementationType)) BpmnXMLUtil.WriteQualifiedAttribute("type", serviceTask.ImplementationType, xtw);
        if (!string.IsNullOrEmpty(serviceTask.Class)) BpmnXMLUtil.WriteQualifiedAttribute("class", serviceTask.Class, xtw);
        if (!string.IsNullOrEmpty(serviceTask.DelegateExpression)) BpmnXMLUtil.WriteQualifiedAttribute("delegateExpression", serviceTask.DelegateExpression, xtw);
        if (!string.IsNullOrEmpty(serviceTask.Expression)) BpmnXMLUtil.WriteQualifiedAttribute("expression", serviceTask.Expression, xtw);
        if (!string.IsNullOrEmpty(serviceTask.ResultVariableName)) BpmnXMLUtil.WriteQualifiedAttribute("resultVariableName", serviceTask.ResultVariableName, xtw);
        if (!string.IsNullOrEmpty(serviceTask.SkippedExpression)) BpmnXMLUtil.WriteQualifiedAttribute("skipExpression", serviceTask.SkippedExpression, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


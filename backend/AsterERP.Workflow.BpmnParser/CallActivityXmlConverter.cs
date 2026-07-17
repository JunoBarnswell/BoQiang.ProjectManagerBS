using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class CallActivityXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "callActivity" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var callActivity = new BpmnModelNs.CallActivity
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            CalledElement = GetAttributeValue(node, "calledElement") ?? GetAttributeValue(node, "activiti", "calledElement"),
            InheritVariables = GetAttributeValue(node, "activiti", "inheritVariables") == "true",
            SameDeployment = GetAttributeValue(node, "activiti", "sameDeployment") == "true",
            InheritBusinessKey = GetAttributeValue(node, "activiti", "inheritBusinessKey") == "true",
            BusinessKey = GetAttributeValue(node, "activiti", "businessKey"),
            ProcessInstanceName = GetAttributeValue(node, "activiti", "processInstanceName"),
            CalledElementType = GetAttributeValue(node, "activiti", "calledElementType")
        };

        ParseInOutParameters(node, callActivity);
        return callActivity;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var callActivity = (BpmnModelNs.CallActivity)element;
        var el = CreateBpmnElement(document, "callActivity");
        SetAttribute(el, "id", callActivity.Id);
        SetAttribute(el, "name", callActivity.Name);
        SetAttribute(el, "calledElement", callActivity.CalledElement);
        if (callActivity.InheritVariables)
            SetAttribute(el, "activiti", "inheritVariables", "true", BpmnConstants.WorkflowExtensionNamespace);
        if (callActivity.SameDeployment)
            SetAttribute(el, "activiti", "sameDeployment", "true", BpmnConstants.WorkflowExtensionNamespace);
        if (callActivity.InheritBusinessKey)
            SetAttribute(el, "activiti", "inheritBusinessKey", "true", BpmnConstants.WorkflowExtensionNamespace);
        SetAttribute(el, "activiti", "businessKey", callActivity.BusinessKey, BpmnConstants.WorkflowExtensionNamespace);
        SetAttribute(el, "activiti", "processInstanceName", callActivity.ProcessInstanceName, BpmnConstants.WorkflowExtensionNamespace);
        SetAttribute(el, "activiti", "calledElementType", callActivity.CalledElementType, BpmnConstants.WorkflowExtensionNamespace);

        WriteInOutParameters(callActivity, el, document);
        parentElement.AppendChild(el);
    }

    private static void ParseInOutParameters(XmlNode node, BpmnModelNs.CallActivity callActivity)
    {
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element || child.LocalName != "extensionElements")
                continue;

            foreach (XmlNode extChild in child.ChildNodes)
            {
                if (extChild.NodeType != XmlNodeType.Element)
                    continue;

                if (extChild.LocalName == "in")
                {
                    callActivity.InParameters.Add(new BpmnModelNs.IOParameter
                    {
                        Source = GetAttributeValue(extChild, "source"),
                        SourceExpression = GetAttributeValue(extChild, "sourceExpression"),
                        Target = GetAttributeValue(extChild, "target"),
                        TargetExpression = GetAttributeValue(extChild, "targetExpression")
                    });
                }
                else if (extChild.LocalName == "out")
                {
                    callActivity.OutParameters.Add(new BpmnModelNs.IOParameter
                    {
                        Source = GetAttributeValue(extChild, "source"),
                        SourceExpression = GetAttributeValue(extChild, "sourceExpression"),
                        Target = GetAttributeValue(extChild, "target"),
                        TargetExpression = GetAttributeValue(extChild, "targetExpression")
                    });
                }
            }
        }
    }

    private static void WriteInOutParameters(BpmnModelNs.CallActivity callActivity, XmlElement callActivityElement, XmlDocument document)
    {
        var hasIn = callActivity.InParameters.Any(p => HasInOutValues(p));
        var hasOut = callActivity.OutParameters.Any(p => HasInOutValues(p));
        if (!hasIn && !hasOut)
            return;

        var extensionElements = CreateBpmnElement(document, "extensionElements");

        foreach (var inParameter in callActivity.InParameters.Where(HasInOutValues))
        {
            var inElement = CreateWorkflowExtensionElement(document, "in");
            SetAttribute(inElement, "source", inParameter.Source);
            SetAttribute(inElement, "sourceExpression", inParameter.SourceExpression);
            SetAttribute(inElement, "target", inParameter.Target);
            SetAttribute(inElement, "targetExpression", inParameter.TargetExpression);
            extensionElements.AppendChild(inElement);
        }

        foreach (var outParameter in callActivity.OutParameters.Where(HasInOutValues))
        {
            var outElement = CreateWorkflowExtensionElement(document, "out");
            SetAttribute(outElement, "source", outParameter.Source);
            SetAttribute(outElement, "sourceExpression", outParameter.SourceExpression);
            SetAttribute(outElement, "target", outParameter.Target);
            SetAttribute(outElement, "targetExpression", outParameter.TargetExpression);
            extensionElements.AppendChild(outElement);
        }

        callActivityElement.AppendChild(extensionElements);
    }

    private static bool HasInOutValues(BpmnModelNs.IOParameter parameter)
    {
        return !string.IsNullOrWhiteSpace(parameter.Source) ||
               !string.IsNullOrWhiteSpace(parameter.SourceExpression) ||
               !string.IsNullOrWhiteSpace(parameter.Target) ||
               !string.IsNullOrWhiteSpace(parameter.TargetExpression);
    }
}


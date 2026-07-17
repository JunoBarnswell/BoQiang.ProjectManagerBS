using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class UserTaskXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "userTask" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var userTask = new BpmnModelNs.UserTask
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            Assignee = GetAttributeValue(node, "activiti", "assignee"),
            CandidateUsers = ParseCommaSeparatedList(GetAttributeValue(node, "activiti", "candidateUsers")),
            CandidateGroups = ParseCommaSeparatedList(GetAttributeValue(node, "activiti", "candidateGroups")),
            FormKey = GetAttributeValue(node, "activiti", "formKey"),
            Priority = int.TryParse(GetAttributeValue(node, "activiti", "priority"), out var p) ? p : null
        };

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "multiInstanceLoopCharacteristics")
            {
                userTask.LoopCharacteristics = ParseMultiInstanceLoopCharacteristics(child);
            }
        }

        return userTask;
    }

    private static BpmnModelNs.MultiInstanceLoopCharacteristics ParseMultiInstanceLoopCharacteristics(XmlNode node)
    {
        var mi = new BpmnModelNs.MultiInstanceLoopCharacteristics
        {
            IsSequential = GetAttributeValue(node, "isSequential") == "true",
            Collection = GetAttributeValue(node, "activiti", "collection"),
            CollectionVariable = GetAttributeValue(node, "activiti", "collectionVariable"),
            ElementVariable = GetAttributeValue(node, "activiti", "elementVariable"),
            ElementIndexVariable = GetAttributeValue(node, "activiti", "elementIndexVariable")
        };

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "completionCondition")
                mi.CompletionCondition = child.InnerText;
            if (child.LocalName == "loopCardinality")
                mi.LoopCardinality = child.InnerText;
        }

        return mi;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var userTask = (BpmnModelNs.UserTask)element;
        var el = CreateBpmnElement(document, "userTask");
        SetAttribute(el, "id", userTask.Id);
        SetAttribute(el, "name", userTask.Name);
        if (userTask.Assignee != null)
            SetAttribute(el, "activiti", "assignee", userTask.Assignee, BpmnConstants.WorkflowExtensionNamespace);
        if (userTask.CandidateUsers is { Count: > 0 })
            SetAttribute(el, "activiti", "candidateUsers", string.Join(",", userTask.CandidateUsers), BpmnConstants.WorkflowExtensionNamespace);
        if (userTask.CandidateGroups is { Count: > 0 })
            SetAttribute(el, "activiti", "candidateGroups", string.Join(",", userTask.CandidateGroups), BpmnConstants.WorkflowExtensionNamespace);
        if (userTask.FormKey != null)
            SetAttribute(el, "activiti", "formKey", userTask.FormKey, BpmnConstants.WorkflowExtensionNamespace);
        if (userTask.Priority.HasValue)
            SetAttribute(el, "activiti", "priority", userTask.Priority.Value.ToString(), BpmnConstants.WorkflowExtensionNamespace);
        parentElement.AppendChild(el);
    }
}


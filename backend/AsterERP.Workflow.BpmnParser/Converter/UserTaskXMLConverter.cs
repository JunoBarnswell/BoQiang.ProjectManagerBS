using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class UserTaskXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.UserTask);
    public override string GetXMLElementName() => "userTask";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var userTask = new BpmnModelNs.UserTask
        {
            Assignee = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "assignee"),
            Owner = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "owner"),
            FormKey = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "formKey"),
            Category = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "category"),
            SkipExpression = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "skipExpression")
        };
        var candidateUsers = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "candidateUsers");
        if (!string.IsNullOrEmpty(candidateUsers)) userTask.CandidateUsers = ParseDelimitedList(candidateUsers);
        var candidateGroups = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "candidateGroups");
        if (!string.IsNullOrEmpty(candidateGroups)) userTask.CandidateGroups = ParseDelimitedList(candidateGroups);
        var priority = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "priority");
        if (int.TryParse(priority, out var p)) userTask.Priority = p;
        ParseChildElements("userTask", userTask, xmlNode, model);
        return userTask;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var userTask = (BpmnModelNs.UserTask)element;
        if (!string.IsNullOrEmpty(userTask.Assignee)) BpmnXMLUtil.WriteQualifiedAttribute("assignee", userTask.Assignee, xtw);
        if (!string.IsNullOrEmpty(userTask.Owner)) BpmnXMLUtil.WriteQualifiedAttribute("owner", userTask.Owner, xtw);
        if (userTask.CandidateUsers.Count > 0) BpmnXMLUtil.WriteQualifiedAttribute("candidateUsers", ConvertToDelimitedString(userTask.CandidateUsers), xtw);
        if (userTask.CandidateGroups.Count > 0) BpmnXMLUtil.WriteQualifiedAttribute("candidateGroups", ConvertToDelimitedString(userTask.CandidateGroups), xtw);
        if (!string.IsNullOrEmpty(userTask.FormKey)) BpmnXMLUtil.WriteQualifiedAttribute("formKey", userTask.FormKey, xtw);
        if (!string.IsNullOrEmpty(userTask.Category)) BpmnXMLUtil.WriteQualifiedAttribute("category", userTask.Category, xtw);
        if (userTask.Priority.HasValue) BpmnXMLUtil.WriteQualifiedAttribute("priority", userTask.Priority.Value.ToString(), xtw);
        if (!string.IsNullOrEmpty(userTask.SkipExpression)) BpmnXMLUtil.WriteQualifiedAttribute("skipExpression", userTask.SkipExpression, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var serviceTask = (BpmnModelNs.ServiceTask)element;
        foreach (var ioSpecification in serviceTask.IOSpecification)
        {
            WriteServiceTaskIOSpecification(ioSpecification, xtw);
            foreach (var association in ioSpecification.InputOutputAssociations)
            {
                WriteServiceTaskAssociation(association, xtw);
            }
        }
    }

    private static void WriteServiceTaskIOSpecification(BpmnModelNs.IOSpecification ioSpecification, XmlWriter xtw)
    {
        var hasIoData = ioSpecification.DataInputs.Count > 0 ||
                        ioSpecification.DataOutputs.Count > 0 ||
                        ioSpecification.DataInputRefs.Count > 0 ||
                        ioSpecification.DataOutputRefs.Count > 0;
        if (!hasIoData)
            return;

        xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "ioSpecification", BpmnXMLConstants.BPMN2_NAMESPACE);

        foreach (var dataInput in ioSpecification.DataInputs)
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "dataInput", BpmnXMLConstants.BPMN2_NAMESPACE);
            BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_ID, dataInput.Id, xtw);
            BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_NAME, dataInput.Name, xtw);
            BpmnXMLUtil.WriteDefaultAttribute("itemSubjectRef", dataInput.ItemSubjectRef, xtw);
            xtw.WriteEndElement();
        }

        foreach (var dataOutput in ioSpecification.DataOutputs)
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "dataOutput", BpmnXMLConstants.BPMN2_NAMESPACE);
            BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_ID, dataOutput.Id, xtw);
            BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_NAME, dataOutput.Name, xtw);
            BpmnXMLUtil.WriteDefaultAttribute("itemSubjectRef", dataOutput.ItemSubjectRef, xtw);
            xtw.WriteEndElement();
        }

        xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "inputSet", BpmnXMLConstants.BPMN2_NAMESPACE);
        foreach (var dataInputRef in ioSpecification.DataInputRefs.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "dataInputRefs", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteString(dataInputRef);
            xtw.WriteEndElement();
        }
        xtw.WriteEndElement();

        xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "outputSet", BpmnXMLConstants.BPMN2_NAMESPACE);
        foreach (var dataOutputRef in ioSpecification.DataOutputRefs.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "dataOutputRefs", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteString(dataOutputRef);
            xtw.WriteEndElement();
        }
        xtw.WriteEndElement();

        xtw.WriteEndElement();
    }

    private static void WriteServiceTaskAssociation(BpmnModelNs.InputOutputAssociation association, XmlWriter xtw)
    {
        var elementName = association.IsInputAssociation
            ? BpmnXMLConstants.ELEMENT_DATA_INPUT_ASSOCIATION
            : BpmnXMLConstants.ELEMENT_DATA_OUTPUT_ASSOCIATION;

        xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, elementName, BpmnXMLConstants.BPMN2_NAMESPACE);
        BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_ID, association.Id, xtw);

        if (!string.IsNullOrWhiteSpace(association.SourceRef))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "sourceRef", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteString(association.SourceRef);
            xtw.WriteEndElement();
        }

        if (!string.IsNullOrWhiteSpace(association.TargetRef))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "targetRef", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteString(association.TargetRef);
            xtw.WriteEndElement();
        }

        if (!string.IsNullOrWhiteSpace(association.Transformation))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "transformation", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteString(association.Transformation);
            xtw.WriteEndElement();
        }

        foreach (var assignment in association.Assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.From) && string.IsNullOrWhiteSpace(assignment.To))
                continue;

            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "assignment", BpmnXMLConstants.BPMN2_NAMESPACE);
            if (!string.IsNullOrWhiteSpace(assignment.From))
            {
                xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "from", BpmnXMLConstants.BPMN2_NAMESPACE);
                xtw.WriteString(assignment.From);
                xtw.WriteEndElement();
            }
            if (!string.IsNullOrWhiteSpace(assignment.To))
            {
                xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "to", BpmnXMLConstants.BPMN2_NAMESPACE);
                xtw.WriteString(assignment.To);
                xtw.WriteEndElement();
            }
            xtw.WriteEndElement();
        }

        xtw.WriteEndElement();
    }
}


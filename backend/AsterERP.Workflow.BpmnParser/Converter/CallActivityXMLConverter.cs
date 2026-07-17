using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class CallActivityXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.CallActivity);
    public override string GetXMLElementName() => "callActivity";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var callActivity = new BpmnModelNs.CallActivity
        {
            CalledElement = GetAttributeValue(xmlNode, "calledElement") ?? GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "calledElement"),
            InheritVariables = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "inheritVariables")?.ToLowerInvariant() == "true",
            SameDeployment = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "sameDeployment")?.ToLowerInvariant() == "true"
        };
        ParseChildElements("callActivity", callActivity, xmlNode, model);
        return callActivity;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var callActivity = (BpmnModelNs.CallActivity)element;
        BpmnXMLUtil.WriteDefaultAttribute("calledElement", callActivity.CalledElement, xtw);
        if (callActivity.InheritVariables) BpmnXMLUtil.WriteQualifiedAttribute("inheritVariables", "true", xtw);
        if (callActivity.SameDeployment) BpmnXMLUtil.WriteQualifiedAttribute("sameDeployment", "true", xtw);
    }

    protected override bool WriteExtensionChildElements(BpmnModelNs.BaseElement element, bool didWriteExtensionStartElement, XmlWriter xtw)
    {
        var callActivity = (BpmnModelNs.CallActivity)element;
        didWriteExtensionStartElement = WriteIOParameters("in", callActivity.InParameters, didWriteExtensionStartElement, xtw);
        didWriteExtensionStartElement = WriteIOParameters("out", callActivity.OutParameters, didWriteExtensionStartElement, xtw);
        return didWriteExtensionStartElement;
    }

    private static bool WriteIOParameters(
        string elementName,
        List<BpmnModelNs.IOParameter> parameterList,
        bool didWriteExtensionStartElement,
        XmlWriter xtw)
    {
        if (parameterList.Count == 0)
            return didWriteExtensionStartElement;

        foreach (var ioParameter in parameterList)
        {
            if (string.IsNullOrWhiteSpace(ioParameter.Source) &&
                string.IsNullOrWhiteSpace(ioParameter.SourceExpression) &&
                string.IsNullOrWhiteSpace(ioParameter.Target) &&
                string.IsNullOrWhiteSpace(ioParameter.TargetExpression))
            {
                continue;
            }

            if (!didWriteExtensionStartElement)
            {
                xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EXTENSIONS);
                didWriteExtensionStartElement = true;
            }

            xtw.WriteStartElement(
                BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX,
                elementName,
                BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE);

            BpmnXMLUtil.WriteDefaultAttribute("source", ioParameter.Source, xtw);
            BpmnXMLUtil.WriteDefaultAttribute("sourceExpression", ioParameter.SourceExpression, xtw);
            BpmnXMLUtil.WriteDefaultAttribute("target", ioParameter.Target, xtw);
            BpmnXMLUtil.WriteDefaultAttribute("targetExpression", ioParameter.TargetExpression, xtw);

            xtw.WriteEndElement();
        }

        return didWriteExtensionStartElement;
    }

    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var callActivity = (BpmnModelNs.CallActivity)element;
        foreach (var ioSpecification in callActivity.IOSpecification)
        {
            WriteIOSpecification(ioSpecification, xtw);
            foreach (var association in ioSpecification.InputOutputAssociations)
            {
                WriteAssociation(association, xtw);
            }
        }
    }

    private static void WriteAssociation(BpmnModelNs.InputOutputAssociation association, XmlWriter xtw)
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

    private static void WriteIOSpecification(BpmnModelNs.IOSpecification ioSpecification, XmlWriter xtw)
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
}


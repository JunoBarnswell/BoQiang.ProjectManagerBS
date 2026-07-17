using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class ValuedDataObjectXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "dataObject" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var structureRef = GetAttributeValue(node, "itemSubjectRef");
        var dataObject = BpmnModelNs.BpmnDataObjects.CreateDataObject(structureRef);
        dataObject.Id = GetAttributeValue(node, "id");
        dataObject.Name = GetAttributeValue(node, "name");
        dataObject.ItemSubjectRef = structureRef;

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element || child.LocalName != "extensionElements")
                continue;

            foreach (XmlNode extensionChild in child.ChildNodes)
            {
                if (extensionChild.NodeType != XmlNodeType.Element || extensionChild.LocalName != "value")
                    continue;
                BpmnModelNs.BpmnDataObjects.SetDataObjectValue(dataObject, extensionChild.InnerText);
            }
        }

        return dataObject;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var dataObject = (BpmnModelNs.ValuedDataObject)element;
        var el = CreateBpmnElement(document, "dataObject");
        SetAttribute(el, "id", dataObject.Id);
        SetAttribute(el, "name", dataObject.Name);
        SetAttribute(el, "itemSubjectRef", dataObject.ItemSubjectRef);

        if (dataObject.Value != null)
        {
            var extensionElements = CreateBpmnElement(document, "extensionElements");
            var valueElement = CreateWorkflowExtensionElement(document, "value");
            valueElement.InnerText = dataObject.Value.ToString() ?? string.Empty;
            extensionElements.AppendChild(valueElement);
            el.AppendChild(extensionElements);
        }

        parentElement.AppendChild(el);
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class DataStoreReferenceXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "dataStoreReference" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return new BpmnModelNs.DataStoreReference
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            DataStoreRef = GetAttributeValue(node, "dataStoreRef"),
            ItemSubjectRef = GetAttributeValue(node, "itemSubjectRef")
        };
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var dataStoreRef = (BpmnModelNs.DataStoreReference)element;
        var el = CreateBpmnElement(document, "dataStoreReference");
        SetAttribute(el, "id", dataStoreRef.Id);
        SetAttribute(el, "name", dataStoreRef.Name);
        SetAttribute(el, "dataStoreRef", dataStoreRef.DataStoreRef);
        SetAttribute(el, "itemSubjectRef", dataStoreRef.ItemSubjectRef);
        parentElement.AppendChild(el);
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class ValuedDataObjectXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.ValuedDataObject);
    public override string GetXMLElementName() => "dataObject";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var structureRef = GetAttributeValue(xmlNode, "itemSubjectRef");
        var dataObject = BpmnModelNs.BpmnDataObjects.CreateDataObject(structureRef);
        dataObject.Name = GetAttributeValue(xmlNode, "name");
        dataObject.ItemSubjectRef = structureRef;
        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "extensionElements")
            {
                foreach (XmlNode extChild in child.ChildNodes)
                {
                    if (extChild.NodeType != XmlNodeType.Element) continue;
                    if (extChild.LocalName == "value")
                        BpmnModelNs.BpmnDataObjects.SetDataObjectValue(dataObject, extChild.InnerText);
                }
            }
        }
        return dataObject;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var dataObject = (BpmnModelNs.ValuedDataObject)element;
        BpmnXMLUtil.WriteDefaultAttribute("name", dataObject.Name, xtw);
        BpmnXMLUtil.WriteDefaultAttribute("itemSubjectRef", dataObject.ItemSubjectRef, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var dataObject = (BpmnModelNs.ValuedDataObject)element;
        if (dataObject.Value != null)
        {
            xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EXTENSIONS);
            xtw.WriteStartElement(BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "value", BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE);
            xtw.WriteString(dataObject.Value.ToString());
            xtw.WriteEndElement();
            xtw.WriteEndElement();
        }
    }
}


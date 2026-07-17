using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class AssociationXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.Association);
    public override string GetXMLElementName() => "association";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model) => new BpmnModelNs.Association
    {
        SourceRef = GetAttributeValue(xmlNode, "sourceRef"),
        TargetRef = GetAttributeValue(xmlNode, "targetRef"),
        AssociationDirection = GetAttributeValue(xmlNode, "associationDirection")
    };
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var association = (BpmnModelNs.Association)element;
        BpmnXMLUtil.WriteDefaultAttribute("sourceRef", association.SourceRef, xtw);
        BpmnXMLUtil.WriteDefaultAttribute("targetRef", association.TargetRef, xtw);
        BpmnXMLUtil.WriteDefaultAttribute("associationDirection", association.AssociationDirection, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


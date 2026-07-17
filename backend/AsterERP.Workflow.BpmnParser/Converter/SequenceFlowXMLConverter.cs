using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class SequenceFlowXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.SequenceFlow);
    public override string GetXMLElementName() => "sequenceFlow";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var sf = new BpmnModelNs.SequenceFlow { SourceRef = GetAttributeValue(xmlNode, "sourceRef"), TargetRef = GetAttributeValue(xmlNode, "targetRef") };
        ParseChildElements("sequenceFlow", sf, xmlNode, model);
        return sf;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var sf = (BpmnModelNs.SequenceFlow)element;
        BpmnXMLUtil.WriteDefaultAttribute("sourceRef", sf.SourceRef, xtw);
        BpmnXMLUtil.WriteDefaultAttribute("targetRef", sf.TargetRef, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var sf = (BpmnModelNs.SequenceFlow)element;
        if (!string.IsNullOrEmpty(sf.ConditionExpression))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "conditionExpression", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteString(sf.ConditionExpression);
            xtw.WriteEndElement();
        }
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class CompensateEventDefinitionXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.CompensateEventDefinition);
    public override string GetXMLElementName() => "compensateEventDefinition";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var compensateDef = new BpmnModelNs.CompensateEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            ActivityRef = GetAttributeValue(xmlNode, "activityRef")
        };

        var waitForCompletion = GetAttributeValue(xmlNode, "waitForCompletion");
        if (!string.IsNullOrWhiteSpace(waitForCompletion) && bool.TryParse(waitForCompletion, out var parsedWaitForCompletion))
            compensateDef.WaitForCompletion = parsedWaitForCompletion;

        return compensateDef;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var compensateDef = (BpmnModelNs.CompensateEventDefinition)element;
        BpmnXMLUtil.WriteDefaultAttribute("activityRef", compensateDef.ActivityRef, xtw);
        if (!compensateDef.WaitForCompletion)
            BpmnXMLUtil.WriteDefaultAttribute("waitForCompletion", "false", xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


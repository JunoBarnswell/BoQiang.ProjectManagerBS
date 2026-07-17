using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class ErrorEventDefinitionXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.ErrorEventDefinition);
    public override string GetXMLElementName() => "errorEventDefinition";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        return new BpmnModelNs.ErrorEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            ErrorCode = GetAttributeValue(xmlNode, "errorCode"),
            ErrorHandlerId = GetAttributeValue(xmlNode, "errorRef")
                             ?? GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "errorRef")
        };
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var errorDef = (BpmnModelNs.ErrorEventDefinition)element;
        var errorRef = errorDef.ErrorHandlerId ?? errorDef.ErrorCode;
        BpmnXMLUtil.WriteDefaultAttribute("errorRef", errorRef, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


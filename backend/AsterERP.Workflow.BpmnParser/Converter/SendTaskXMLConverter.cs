using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class SendTaskXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.SendTask);
    public override string GetXMLElementName() => "sendTask";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model) => new BpmnModelNs.SendTask
    {
        Implementation = GetAttributeValue(xmlNode, "implementation"),
        ImplementationType = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "type"),
        OperationRef = GetAttributeValue(xmlNode, "operationRef")
    };
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var sendTask = (BpmnModelNs.SendTask)element;
        BpmnXMLUtil.WriteDefaultAttribute("implementation", sendTask.Implementation, xtw);
        if (!string.IsNullOrEmpty(sendTask.ImplementationType)) BpmnXMLUtil.WriteQualifiedAttribute("type", sendTask.ImplementationType, xtw);
        BpmnXMLUtil.WriteDefaultAttribute("operationRef", sendTask.OperationRef, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}


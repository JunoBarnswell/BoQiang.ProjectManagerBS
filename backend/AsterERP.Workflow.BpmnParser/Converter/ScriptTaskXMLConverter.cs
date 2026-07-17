using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class ScriptTaskXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.ScriptTask);
    public override string GetXMLElementName() => "scriptTask";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var scriptTask = new BpmnModelNs.ScriptTask
        {
            ScriptFormat = GetAttributeValue(xmlNode, "scriptFormat"),
            ResultVariable = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "resultVariable"),
            AutoStoreVariables = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "autoStoreVariables")?.ToLowerInvariant() == "true"
        };
        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "script") scriptTask.Script = child.InnerText;
        }
        return scriptTask;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var scriptTask = (BpmnModelNs.ScriptTask)element;
        BpmnXMLUtil.WriteDefaultAttribute("scriptFormat", scriptTask.ScriptFormat, xtw);
        if (!string.IsNullOrEmpty(scriptTask.ResultVariable)) BpmnXMLUtil.WriteQualifiedAttribute("resultVariable", scriptTask.ResultVariable, xtw);
        if (scriptTask.AutoStoreVariables) BpmnXMLUtil.WriteQualifiedAttribute("autoStoreVariables", "true", xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var scriptTask = (BpmnModelNs.ScriptTask)element;
        if (!string.IsNullOrEmpty(scriptTask.Script))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "script", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteCData(scriptTask.Script);
            xtw.WriteEndElement();
        }
    }
}


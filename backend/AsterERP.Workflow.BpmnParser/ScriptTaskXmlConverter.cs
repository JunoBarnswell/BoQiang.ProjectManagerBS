using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class ScriptTaskXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "scriptTask" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var st = new BpmnModelNs.ScriptTask
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            ScriptFormat = GetAttributeValue(node, "scriptFormat"),
            ResultVariable = GetAttributeValue(node, "activiti", "resultVariable"),
            AutoStoreVariables = GetAttributeValue(node, "activiti", "autoStoreVariables") == "true"
        };
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.LocalName == "script" && child.NodeType == XmlNodeType.Element)
                st.Script = child.InnerText;
        }
        return st;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var scriptTask = (BpmnModelNs.ScriptTask)element;
        var el = CreateBpmnElement(document, "scriptTask");
        SetAttribute(el, "id", scriptTask.Id);
        SetAttribute(el, "name", scriptTask.Name);
        SetAttribute(el, "scriptFormat", scriptTask.ScriptFormat);
        if (scriptTask.ResultVariable != null)
            SetAttribute(el, "activiti", "resultVariable", scriptTask.ResultVariable, BpmnConstants.WorkflowExtensionNamespace);
        if (scriptTask.Script != null)
        {
            var scriptEl = CreateBpmnElement(document, "script");
            scriptEl.InnerText = scriptTask.Script;
            el.AppendChild(scriptEl);
        }
        parentElement.AppendChild(el);
    }
}


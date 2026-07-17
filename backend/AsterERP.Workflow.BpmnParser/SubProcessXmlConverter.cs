using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class SubProcessXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "subProcess", "eventSubProcess" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var triggeredByEvent = GetAttributeValue(node, "triggeredByEvent") == "true";
        var sp = triggeredByEvent
            ? new BpmnModelNs.EventSubProcess()
            : new BpmnModelNs.SubProcess();

        sp.Id = GetAttributeValue(node, "id");
        sp.Name = GetAttributeValue(node, "name");
        sp.TriggeredByEvent = triggeredByEvent;
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            var converter = BpmnConverterRegistry.GetConverter(child.LocalName);
            if (converter != null)
            {
                var fe = converter.ConvertToBpmnModel(child, process);
                if (fe is BpmnModelNs.FlowElement flowElement)
                {
                    flowElement.ParentContainer = sp;
                    sp.FlowElements.Add(flowElement);
                }
            }
            else
                EnsureSupportedChildElement(child, "subProcess");
        }
        return sp;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var sp = (BpmnModelNs.SubProcess)element;
        var el = CreateBpmnElement(document, "subProcess");
        SetAttribute(el, "id", sp.Id);
        SetAttribute(el, "name", sp.Name);
        if (sp.TriggeredByEvent)
            el.SetAttribute("triggeredByEvent", "true");
        foreach (var fe in sp.FlowElements)
        {
            var converter = BpmnConverterRegistry.GetConverterForElement(fe);
            converter?.ConvertToXml(fe, el, document);
        }
        parentElement.AppendChild(el);
    }
}


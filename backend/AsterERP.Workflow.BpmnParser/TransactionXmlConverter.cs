using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class TransactionXmlConverter : BaseBpmnXmlConverter
{
    public override string[] ElementTypes => new[] { "transaction" };

    public override BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        var tx = new BpmnModelNs.Transaction
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            TriggeredByEvent = GetAttributeValue(node, "triggeredByEvent") == "true"
        };
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            var converter = BpmnConverterRegistry.GetConverter(child.LocalName);
            if (converter != null)
            {
                var fe = converter.ConvertToBpmnModel(child, process);
                if (fe is BpmnModelNs.FlowElement flowElement)
                {
                    flowElement.ParentContainer = tx;
                    tx.FlowElements.Add(flowElement);
                }
            }
            else
                EnsureSupportedChildElement(child, "transaction");
        }
        return tx;
    }

    public override void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document)
    {
        var tx = (BpmnModelNs.Transaction)element;
        var el = CreateBpmnElement(document, "transaction");
        SetAttribute(el, "id", tx.Id);
        SetAttribute(el, "name", tx.Name);
        if (tx.TriggeredByEvent)
            el.SetAttribute("triggeredByEvent", "true");
        foreach (var fe in tx.FlowElements)
        {
            var converter = BpmnConverterRegistry.GetConverterForElement(fe);
            converter?.ConvertToXml(fe, el, document);
        }
        parentElement.AppendChild(el);
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class MessageEventDefinitionXmlConverter
{
    public string[] ElementTypes => new[] { "messageEventDefinition" };

    public BpmnModelNs.MessageEventDefinition ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return EventDefinitionConverter.ParseMessageEventDefinition(node);
    }

    public void ConvertToXml(BpmnModelNs.MessageEventDefinition element, XmlElement parentElement, XmlDocument document)
    {
        EventDefinitionConverter.WriteMessageEventDefinition(element, parentElement, document);
    }
}


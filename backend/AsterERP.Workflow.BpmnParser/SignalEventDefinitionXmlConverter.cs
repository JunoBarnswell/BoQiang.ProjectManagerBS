using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class SignalEventDefinitionXmlConverter
{
    public string[] ElementTypes => new[] { "signalEventDefinition" };

    public BpmnModelNs.SignalEventDefinition ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return EventDefinitionConverter.ParseSignalEventDefinition(node);
    }

    public void ConvertToXml(BpmnModelNs.SignalEventDefinition element, XmlElement parentElement, XmlDocument document)
    {
        EventDefinitionConverter.WriteSignalEventDefinition(element, parentElement, document);
    }
}


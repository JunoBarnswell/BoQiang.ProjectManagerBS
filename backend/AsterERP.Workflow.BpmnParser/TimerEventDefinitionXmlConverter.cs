using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class TimerEventDefinitionXmlConverter
{
    public string[] ElementTypes => new[] { "timerEventDefinition" };

    public BpmnModelNs.TimerEventDefinition ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process)
    {
        return EventDefinitionConverter.ParseTimerEventDefinition(node);
    }

    public void ConvertToXml(BpmnModelNs.TimerEventDefinition element, XmlElement parentElement, XmlDocument document)
    {
        EventDefinitionConverter.WriteTimerEventDefinition(element, parentElement, document);
    }
}


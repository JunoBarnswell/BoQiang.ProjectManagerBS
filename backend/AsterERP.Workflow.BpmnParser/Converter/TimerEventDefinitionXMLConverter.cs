using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class TimerEventDefinitionXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.TimerEventDefinition);
    public override string GetXMLElementName() => "timerEventDefinition";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var timerDef = new BpmnModelNs.TimerEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            CalendarName = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "calendarName")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element)
                continue;

            switch (child.LocalName)
            {
                case "timeDate":
                    timerDef.TimeDate = child.InnerText.Trim();
                    break;
                case "timeCycle":
                    timerDef.TimeCycle = child.InnerText.Trim();
                    timerDef.EndDate = GetAttributeValue(child, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "endDate")
                                       ?? GetAttributeValue(child, "endDate");
                    break;
                case "timeDuration":
                    timerDef.TimeDuration = child.InnerText.Trim();
                    break;
            }
        }

        return timerDef;
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var timerDef = (BpmnModelNs.TimerEventDefinition)element;
        BpmnXMLUtil.WriteQualifiedAttribute("calendarName", timerDef.CalendarName, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var timerDef = (BpmnModelNs.TimerEventDefinition)element;
        if (!string.IsNullOrWhiteSpace(timerDef.TimeDate))
        {
            xtw.WriteStartElement("timeDate");
            xtw.WriteString(timerDef.TimeDate);
            xtw.WriteEndElement();
            return;
        }

        if (!string.IsNullOrWhiteSpace(timerDef.TimeCycle))
        {
            xtw.WriteStartElement("timeCycle");
            if (!string.IsNullOrWhiteSpace(timerDef.EndDate))
                BpmnXMLUtil.WriteQualifiedAttribute("endDate", timerDef.EndDate, xtw);
            xtw.WriteString(timerDef.TimeCycle);
            xtw.WriteEndElement();
            return;
        }

        if (!string.IsNullOrWhiteSpace(timerDef.TimeDuration))
        {
            xtw.WriteStartElement("timeDuration");
            xtw.WriteString(timerDef.TimeDuration);
            xtw.WriteEndElement();
        }
    }
}


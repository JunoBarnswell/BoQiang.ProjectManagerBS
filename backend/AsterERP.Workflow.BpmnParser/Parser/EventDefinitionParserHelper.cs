using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public static class EventDefinitionParserHelper
{
    public static EventDefinition? ParseEventDefinition(XmlNode node)
    {
        return node.LocalName switch
        {
            "timerEventDefinition" => ParseTimerEventDefinition(node),
            "signalEventDefinition" => ParseSignalEventDefinition(node),
            "messageEventDefinition" => ParseMessageEventDefinition(node),
            "errorEventDefinition" => ParseErrorEventDefinition(node),
            "escalationEventDefinition" => ParseEscalationEventDefinition(node),
            "conditionalEventDefinition" => ParseConditionalEventDefinition(node),
            "cancelEventDefinition" => ParseCancelEventDefinition(node),
            "compensateEventDefinition" => ParseCompensateEventDefinition(node),
            "terminateEventDefinition" => ParseTerminateEventDefinition(node),
            "linkEventDefinition" => ParseLinkEventDefinition(node),
            _ => null
        };
    }

    public static TimerEventDefinition ParseTimerEventDefinition(XmlNode node)
    {
        var timerDef = new TimerEventDefinition { Id = GetAttributeValue(node, "id") };
        timerDef.CalendarName = GetAttributeValue(node, "activiti", "calendarName");

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            switch (child.LocalName)
            {
                case "timeDate": timerDef.TimeDate = child.InnerText.Trim(); break;
                case "timeCycle":
                    timerDef.TimeCycle = child.InnerText.Trim();
                    timerDef.EndDate = GetAttributeValue(child, "activiti", "endDate") ?? GetAttributeValue(child, "endDate");
                    break;
                case "timeDuration": timerDef.TimeDuration = child.InnerText.Trim(); break;
            }
        }
        return timerDef;
    }

    public static SignalEventDefinition ParseSignalEventDefinition(XmlNode node)
    {
        return new SignalEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            SignalRef = GetAttributeValue(node, "signalRef"),
            Scope = GetAttributeValue(node, "activiti", "scope")
        };
    }

    public static MessageEventDefinition ParseMessageEventDefinition(XmlNode node)
    {
        return new MessageEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            MessageRef = GetAttributeValue(node, "messageRef")
        };
    }

    public static ErrorEventDefinition ParseErrorEventDefinition(XmlNode node)
    {
        return new ErrorEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            ErrorCode = GetAttributeValue(node, "errorCode"),
            ErrorHandlerId = GetAttributeValue(node, "errorRef") ?? GetAttributeValue(node, "activiti", "errorRef")
        };
    }

    public static EscalationEventDefinition ParseEscalationEventDefinition(XmlNode node)
    {
        return new EscalationEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            EscalationRef = GetAttributeValue(node, "escalationRef"),
            EscalationCode = GetAttributeValue(node, "escalationCode")
        };
    }

    public static ConditionalEventDefinition ParseConditionalEventDefinition(XmlNode node)
    {
        var conditionalDef = new ConditionalEventDefinition
        {
            Id = GetAttributeValue(node, "id")
        };

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element)
                continue;

            if (child.LocalName is "condition" or "conditionExpression")
            {
                var condition = child.InnerText.Trim();
                conditionalDef.Condition = condition;
                conditionalDef.ConditionExpression = condition;
            }
        }

        return conditionalDef;
    }

    public static CancelEventDefinition ParseCancelEventDefinition(XmlNode node)
    {
        return new CancelEventDefinition
        {
            Id = GetAttributeValue(node, "id")
        };
    }

    public static CompensateEventDefinition ParseCompensateEventDefinition(XmlNode node)
    {
        var compensateDef = new CompensateEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            WaitForCompletion = GetAttributeValue(node, "waitForCompletion") != "false",
            ActivityRef = GetAttributeValue(node, "activityRef")
        };

        return compensateDef;
    }

    public static TerminateEventDefinition ParseTerminateEventDefinition(XmlNode node)
    {
        return new TerminateEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            TerminateAll = GetAttributeValue(node, "terminateAll") == "true"
        };
    }

    public static LinkEventDefinition ParseLinkEventDefinition(XmlNode node)
    {
        var linkDef = new LinkEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            Target = GetAttributeValue(node, "target")
        };

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element)
                continue;

            if (child.LocalName == "source")
                linkDef.Sources.Add(child.InnerText.Trim());
            else if (child.LocalName == "target")
                linkDef.Target = child.InnerText.Trim();
        }

        return linkDef;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }

    private static string? GetAttributeValue(XmlNode node, string prefix, string localName)
    {
        if (node.Attributes == null) return null;
        foreach (XmlAttribute attr in node.Attributes)
        {
            if (attr.LocalName == localName && MatchesPrefixOrNamespace(attr, prefix))
                return attr.Value;
        }
        return null;
    }

    private static bool MatchesPrefixOrNamespace(XmlAttribute attr, string prefix)
    {
        return attr.Prefix == prefix ||
               (prefix == BpmnConstants.WorkflowExtensionPrefix && attr.NamespaceURI == BpmnConstants.WorkflowExtensionNamespace);
    }
}

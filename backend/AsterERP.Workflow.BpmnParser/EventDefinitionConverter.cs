using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public static class EventDefinitionConverter
{
    public static BpmnModelNs.TimerEventDefinition ParseTimerEventDefinition(XmlNode node)
    {
        var timerDef = new BpmnModelNs.TimerEventDefinition { Id = GetAttributeValue(node, "id") };
        timerDef.CalendarName = GetAttributeValue(node, "activiti", "calendarName");

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            switch (child.LocalName)
            {
                case "timeDate":
                    timerDef.TimeDate = child.InnerText.Trim();
                    break;
                case "timeCycle":
                    timerDef.TimeCycle = child.InnerText.Trim();
                    timerDef.EndDate = GetAttributeValue(child, "activiti", "endDate") ?? GetAttributeValue(child, "endDate");
                    break;
                case "timeDuration":
                    timerDef.TimeDuration = child.InnerText.Trim();
                    break;
            }
        }
        return timerDef;
    }

    public static BpmnModelNs.SignalEventDefinition ParseSignalEventDefinition(XmlNode node)
    {
        return new BpmnModelNs.SignalEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            SignalRef = GetAttributeValue(node, "signalRef"),
            Scope = GetAttributeValue(node, "activiti", "scope")
        };
    }

    public static BpmnModelNs.MessageEventDefinition ParseMessageEventDefinition(XmlNode node)
    {
        return new BpmnModelNs.MessageEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            MessageRef = GetAttributeValue(node, "messageRef")
        };
    }

    public static BpmnModelNs.ErrorEventDefinition ParseErrorEventDefinition(XmlNode node)
    {
        return new BpmnModelNs.ErrorEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            ErrorCode = GetAttributeValue(node, "errorCode"),
            ErrorHandlerId = GetAttributeValue(node, "errorRef") ?? GetAttributeValue(node, "activiti", "errorRef")
        };
    }

    public static BpmnModelNs.EscalationEventDefinition ParseEscalationEventDefinition(XmlNode node)
    {
        return new BpmnModelNs.EscalationEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            EscalationRef = GetAttributeValue(node, "escalationRef"),
            EscalationCode = GetAttributeValue(node, "escalationCode")
        };
    }

    public static BpmnModelNs.ConditionalEventDefinition ParseConditionalEventDefinition(XmlNode node)
    {
        var conditionalDef = new BpmnModelNs.ConditionalEventDefinition
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

    public static BpmnModelNs.CancelEventDefinition ParseCancelEventDefinition(XmlNode node)
    {
        return new BpmnModelNs.CancelEventDefinition
        {
            Id = GetAttributeValue(node, "id")
        };
    }

    public static BpmnModelNs.CompensateEventDefinition ParseCompensateEventDefinition(XmlNode node)
    {
        var compensateDef = new BpmnModelNs.CompensateEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            ActivityRef = GetAttributeValue(node, "activityRef")
        };

        var waitForCompletion = GetAttributeValue(node, "waitForCompletion");
        if (!string.IsNullOrWhiteSpace(waitForCompletion) && bool.TryParse(waitForCompletion, out var parsedWaitForCompletion))
            compensateDef.WaitForCompletion = parsedWaitForCompletion;

        return compensateDef;
    }

    public static BpmnModelNs.TerminateEventDefinition ParseTerminateEventDefinition(XmlNode node)
    {
        return new BpmnModelNs.TerminateEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            TerminateAll = GetAttributeValue(node, "terminateAll") == "true"
        };
    }

    public static BpmnModelNs.LinkEventDefinition ParseLinkEventDefinition(XmlNode node)
    {
        var linkDef = new BpmnModelNs.LinkEventDefinition
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
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

    public static void WriteTimerEventDefinition(BpmnModelNs.TimerEventDefinition timerDef, XmlElement parentElement, XmlDocument document)
    {
        var el = document.CreateElement("bpmn", "timerEventDefinition", BpmnConstants.BpmnNamespace);
        if (timerDef.Id != null)
            el.SetAttribute("id", timerDef.Id);
        if (timerDef.CalendarName != null)
            SetWorkflowExtensionAttribute(el, "calendarName", timerDef.CalendarName);
        if (timerDef.TimeDate != null)
        {
            var child = document.CreateElement("bpmn", "timeDate", BpmnConstants.BpmnNamespace);
            child.InnerText = timerDef.TimeDate;
            el.AppendChild(child);
        }
        else if (timerDef.TimeCycle != null)
        {
            var child = document.CreateElement("bpmn", "timeCycle", BpmnConstants.BpmnNamespace);
            if (timerDef.EndDate != null)
                SetWorkflowExtensionAttribute(child, "endDate", timerDef.EndDate);
            child.InnerText = timerDef.TimeCycle;
            el.AppendChild(child);
        }
        else if (timerDef.TimeDuration != null)
        {
            var child = document.CreateElement("bpmn", "timeDuration", BpmnConstants.BpmnNamespace);
            child.InnerText = timerDef.TimeDuration;
            el.AppendChild(child);
        }
        parentElement.AppendChild(el);
    }

    public static void WriteSignalEventDefinition(BpmnModelNs.SignalEventDefinition signalDef, XmlElement parentElement, XmlDocument document)
    {
        var el = document.CreateElement("bpmn", "signalEventDefinition", BpmnConstants.BpmnNamespace);
        if (signalDef.Id != null)
            el.SetAttribute("id", signalDef.Id);
        if (signalDef.SignalRef != null)
            el.SetAttribute("signalRef", signalDef.SignalRef);
        parentElement.AppendChild(el);
    }

    public static void WriteMessageEventDefinition(BpmnModelNs.MessageEventDefinition messageDef, XmlElement parentElement, XmlDocument document)
    {
        var el = document.CreateElement("bpmn", "messageEventDefinition", BpmnConstants.BpmnNamespace);
        if (messageDef.Id != null)
            el.SetAttribute("id", messageDef.Id);
        if (messageDef.MessageRef != null)
            el.SetAttribute("messageRef", messageDef.MessageRef);
        parentElement.AppendChild(el);
    }

    public static void WriteErrorEventDefinition(BpmnModelNs.ErrorEventDefinition errorDef, XmlElement parentElement, XmlDocument document)
    {
        var el = document.CreateElement("bpmn", "errorEventDefinition", BpmnConstants.BpmnNamespace);
        if (errorDef.Id != null)
            el.SetAttribute("id", errorDef.Id);
        var errorRef = errorDef.ErrorHandlerId ?? errorDef.ErrorCode;
        if (errorRef != null)
            el.SetAttribute("errorRef", errorRef);
        parentElement.AppendChild(el);
    }

    public static void WriteEscalationEventDefinition(BpmnModelNs.EscalationEventDefinition escalationDef, XmlElement parentElement, XmlDocument document)
    {
        var el = document.CreateElement("bpmn", "escalationEventDefinition", BpmnConstants.BpmnNamespace);
        if (escalationDef.Id != null)
            el.SetAttribute("id", escalationDef.Id);
        if (escalationDef.EscalationRef != null)
            el.SetAttribute("escalationRef", escalationDef.EscalationRef);
        if (escalationDef.EscalationCode != null)
            el.SetAttribute("escalationCode", escalationDef.EscalationCode);
        parentElement.AppendChild(el);
    }

    public static void WriteConditionalEventDefinition(BpmnModelNs.ConditionalEventDefinition conditionalDef, XmlElement parentElement, XmlDocument document)
    {
        var el = document.CreateElement("bpmn", "conditionalEventDefinition", BpmnConstants.BpmnNamespace);
        if (conditionalDef.Id != null)
            el.SetAttribute("id", conditionalDef.Id);

        var condition = conditionalDef.ConditionExpression ?? conditionalDef.Condition;
        if (!string.IsNullOrWhiteSpace(condition))
        {
            var conditionElement = document.CreateElement("bpmn", "condition", BpmnConstants.BpmnNamespace);
            conditionElement.InnerText = condition;
            el.AppendChild(conditionElement);
        }

        parentElement.AppendChild(el);
    }

    public static void WriteCancelEventDefinition(BpmnModelNs.CancelEventDefinition cancelDef, XmlElement parentElement, XmlDocument document)
    {
        var el = document.CreateElement("bpmn", "cancelEventDefinition", BpmnConstants.BpmnNamespace);
        if (cancelDef.Id != null)
            el.SetAttribute("id", cancelDef.Id);
        parentElement.AppendChild(el);
    }

    public static void WriteCompensateEventDefinition(BpmnModelNs.CompensateEventDefinition compensateDef, XmlElement parentElement, XmlDocument document)
    {
        var el = document.CreateElement("bpmn", "compensateEventDefinition", BpmnConstants.BpmnNamespace);
        if (compensateDef.Id != null)
            el.SetAttribute("id", compensateDef.Id);
        if (!string.IsNullOrWhiteSpace(compensateDef.ActivityRef))
            el.SetAttribute("activityRef", compensateDef.ActivityRef);
        if (!compensateDef.WaitForCompletion)
            el.SetAttribute("waitForCompletion", "false");
        parentElement.AppendChild(el);
    }

    public static void WriteTerminateEventDefinition(BpmnModelNs.TerminateEventDefinition terminateDef, XmlElement parentElement, XmlDocument document)
    {
        var el = document.CreateElement("bpmn", "terminateEventDefinition", BpmnConstants.BpmnNamespace);
        if (terminateDef.Id != null)
            el.SetAttribute("id", terminateDef.Id);
        if (terminateDef.TerminateAll)
            el.SetAttribute("terminateAll", "true");
        parentElement.AppendChild(el);
    }

    public static void WriteLinkEventDefinition(BpmnModelNs.LinkEventDefinition linkDef, XmlElement parentElement, XmlDocument document)
    {
        var el = document.CreateElement("bpmn", "linkEventDefinition", BpmnConstants.BpmnNamespace);
        if (linkDef.Id != null)
            el.SetAttribute("id", linkDef.Id);
        if (linkDef.Name != null)
            el.SetAttribute("name", linkDef.Name);

        foreach (var source in linkDef.Sources.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var sourceElement = document.CreateElement("bpmn", "source", BpmnConstants.BpmnNamespace);
            sourceElement.InnerText = source;
            el.AppendChild(sourceElement);
        }

        if (!string.IsNullOrWhiteSpace(linkDef.Target))
        {
            var targetElement = document.CreateElement("bpmn", "target", BpmnConstants.BpmnNamespace);
            targetElement.InnerText = linkDef.Target;
            el.AppendChild(targetElement);
        }

        parentElement.AppendChild(el);
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }

    private static void SetWorkflowExtensionAttribute(XmlElement element, string localName, string value)
    {
        var attr = element.OwnerDocument.CreateAttribute("activiti", localName, BpmnConstants.WorkflowExtensionNamespace);
        attr.Value = value;
        element.SetAttributeNode(attr);
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


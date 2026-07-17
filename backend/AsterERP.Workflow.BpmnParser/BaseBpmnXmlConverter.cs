using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public abstract class BaseBpmnXmlConverter
{
    public abstract string[] ElementTypes { get; }
    public abstract BpmnModelNs.BaseElement ConvertToBpmnModel(XmlNode node, BpmnModelNs.Process process);
    public abstract void ConvertToXml(BpmnModelNs.BaseElement element, XmlElement parentElement, XmlDocument document);

    protected static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }

    protected static string? GetAttributeValue(XmlNode node, string prefix, string localName)
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

    protected static List<string> ParseCommaSeparatedList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<string>();
        return value.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    protected static void SetAttribute(XmlElement element, string localName, string? value)
    {
        if (value != null)
            element.SetAttribute(localName, value);
    }

    protected static void SetAttribute(XmlElement element, string prefix, string localName, string? value, string ns)
    {
        if (value != null)
        {
            var attr = element.OwnerDocument?.CreateAttribute(prefix, localName, ns);
            if (attr != null)
            {
                attr.Value = value;
                element.SetAttributeNode(attr);
            }
        }
    }

    protected static XmlElement CreateBpmnElement(XmlDocument document, string localName)
    {
        return document.CreateElement("bpmn", localName, BpmnConstants.BpmnNamespace);
    }

    protected static XmlElement CreateWorkflowExtensionElement(XmlDocument document, string localName)
    {
        return document.CreateElement("activiti", localName, BpmnConstants.WorkflowExtensionNamespace);
    }

    protected static void WriteEventDefinitions(BpmnModelNs.Event eventObj, XmlElement parentElement, XmlDocument document)
    {
        List<BpmnModelNs.EventDefinition>? definitions = null;
        if (eventObj is BpmnModelNs.CatchEvent catchEvent)
            definitions = catchEvent.EventDefinitions;
        else if (eventObj is BpmnModelNs.ThrowEvent throwEvent)
            definitions = throwEvent.EventDefinitions;

        if (eventObj is BpmnModelNs.BoundaryEvent boundary)
        {
            var isSingleErrorDefinition = definitions?.Count == 1 && definitions[0] is BpmnModelNs.ErrorEventDefinition;
            if (definitions?.Count == 1 && !isSingleErrorDefinition)
                parentElement.SetAttribute("cancelActivity", boundary.CancelActivity.ToString().ToLowerInvariant());

            var attachedToRef = boundary.AttachedToRef?.Id ?? boundary.AttachedToRefId;
            if (!string.IsNullOrEmpty(attachedToRef))
                parentElement.SetAttribute("attachedToRef", attachedToRef);
        }

        if (definitions == null) return;

        foreach (var def in definitions)
        {
            ValidateEventDefinitionForEventType(eventObj, def);

            if (def is BpmnModelNs.TimerEventDefinition timerDef)
                EventDefinitionConverter.WriteTimerEventDefinition(timerDef, parentElement, document);
            else if (def is BpmnModelNs.SignalEventDefinition signalDef)
                EventDefinitionConverter.WriteSignalEventDefinition(signalDef, parentElement, document);
            else if (def is BpmnModelNs.MessageEventDefinition messageDef)
                EventDefinitionConverter.WriteMessageEventDefinition(messageDef, parentElement, document);
            else if (def is BpmnModelNs.ErrorEventDefinition errorDef)
                EventDefinitionConverter.WriteErrorEventDefinition(errorDef, parentElement, document);
            else if (def is BpmnModelNs.EscalationEventDefinition escalationDef)
                EventDefinitionConverter.WriteEscalationEventDefinition(escalationDef, parentElement, document);
            else if (def is BpmnModelNs.ConditionalEventDefinition conditionalDef)
                EventDefinitionConverter.WriteConditionalEventDefinition(conditionalDef, parentElement, document);
            else if (def is BpmnModelNs.CancelEventDefinition cancelDef)
                EventDefinitionConverter.WriteCancelEventDefinition(cancelDef, parentElement, document);
            else if (def is BpmnModelNs.CompensateEventDefinition compensateDef)
                EventDefinitionConverter.WriteCompensateEventDefinition(compensateDef, parentElement, document);
            else if (def is BpmnModelNs.TerminateEventDefinition terminateDef)
                EventDefinitionConverter.WriteTerminateEventDefinition(terminateDef, parentElement, document);
            else if (def is BpmnModelNs.LinkEventDefinition linkDef)
                EventDefinitionConverter.WriteLinkEventDefinition(linkDef, parentElement, document);
            else
                throw new WorkflowEngineException($"Unsupported event definition '{def.GetType().Name}'.");
        }
    }

    protected static void ParseEventDefinitions(XmlNode node, BpmnModelNs.Event eventObj)
    {
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;

            if (child.LocalName == "timerEventDefinition")
            {
                var timerDef = EventDefinitionConverter.ParseTimerEventDefinition(child);
                AddEventDefinition(eventObj, timerDef);
            }
            else if (child.LocalName == "signalEventDefinition")
            {
                var signalDef = EventDefinitionConverter.ParseSignalEventDefinition(child);
                AddEventDefinition(eventObj, signalDef);
            }
            else if (child.LocalName == "messageEventDefinition")
            {
                var messageDef = EventDefinitionConverter.ParseMessageEventDefinition(child);
                AddEventDefinition(eventObj, messageDef);
            }
            else if (child.LocalName == "errorEventDefinition")
            {
                var errorDef = EventDefinitionConverter.ParseErrorEventDefinition(child);
                AddEventDefinition(eventObj, errorDef);
            }
            else if (child.LocalName == "escalationEventDefinition")
            {
                var escalationDef = EventDefinitionConverter.ParseEscalationEventDefinition(child);
                AddEventDefinition(eventObj, escalationDef);
            }
            else if (child.LocalName == "conditionalEventDefinition")
            {
                var conditionalDef = EventDefinitionConverter.ParseConditionalEventDefinition(child);
                AddEventDefinition(eventObj, conditionalDef);
            }
            else if (child.LocalName == "cancelEventDefinition")
            {
                var cancelDef = EventDefinitionConverter.ParseCancelEventDefinition(child);
                AddEventDefinition(eventObj, cancelDef);
            }
            else if (child.LocalName == "compensateEventDefinition")
            {
                var compensateDef = EventDefinitionConverter.ParseCompensateEventDefinition(child);
                AddEventDefinition(eventObj, compensateDef);
            }
            else if (child.LocalName == "terminateEventDefinition")
            {
                var terminateDef = EventDefinitionConverter.ParseTerminateEventDefinition(child);
                AddEventDefinition(eventObj, terminateDef);
            }
            else if (child.LocalName == "linkEventDefinition")
            {
                var linkDef = EventDefinitionConverter.ParseLinkEventDefinition(child);
                AddEventDefinition(eventObj, linkDef);
            }
        }
    }

    private static void AddEventDefinition(BpmnModelNs.Event eventObj, BpmnModelNs.EventDefinition def)
    {
        if (eventObj is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (eventObj is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }

    private static void ValidateEventDefinitionForEventType(BpmnModelNs.Event eventObj, BpmnModelNs.EventDefinition def)
    {
        var valid = eventObj switch
        {
            BpmnModelNs.StartEvent => def is BpmnModelNs.TimerEventDefinition or BpmnModelNs.SignalEventDefinition or BpmnModelNs.MessageEventDefinition or BpmnModelNs.ConditionalEventDefinition or BpmnModelNs.ErrorEventDefinition,
            BpmnModelNs.EndEvent => def is BpmnModelNs.ErrorEventDefinition or BpmnModelNs.EscalationEventDefinition or BpmnModelNs.CancelEventDefinition or BpmnModelNs.TerminateEventDefinition,
            BpmnModelNs.BoundaryEvent => def is BpmnModelNs.TimerEventDefinition or BpmnModelNs.SignalEventDefinition or BpmnModelNs.MessageEventDefinition or BpmnModelNs.ErrorEventDefinition or BpmnModelNs.EscalationEventDefinition or BpmnModelNs.ConditionalEventDefinition or BpmnModelNs.CancelEventDefinition or BpmnModelNs.CompensateEventDefinition,
            BpmnModelNs.IntermediateCatchEvent => def is BpmnModelNs.TimerEventDefinition or BpmnModelNs.SignalEventDefinition or BpmnModelNs.MessageEventDefinition or BpmnModelNs.ConditionalEventDefinition or BpmnModelNs.LinkEventDefinition,
            BpmnModelNs.IntermediateThrowEvent => def is BpmnModelNs.SignalEventDefinition or BpmnModelNs.CompensateEventDefinition or BpmnModelNs.LinkEventDefinition or BpmnModelNs.EscalationEventDefinition or BpmnModelNs.MessageEventDefinition,
            _ => true
        };

        if (!valid)
            throw new WorkflowEngineException($"Unsupported event definition '{def.GetType().Name}' for event type '{eventObj.GetType().Name}'.");
    }

    protected static void EnsureSupportedChildElement(XmlNode child, string parentElementType)
    {
        var childId = GetAttributeValue(child, "id") ?? "<unknown>";
        throw new WorkflowEngineException(
            $"Unsupported BPMN element '{child.LocalName}' (id='{childId}') inside '{parentElementType}'.");
    }
}


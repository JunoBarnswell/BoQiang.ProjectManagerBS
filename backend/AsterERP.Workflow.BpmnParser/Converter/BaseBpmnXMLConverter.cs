using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public abstract class BaseBpmnXMLConverter
{
    protected static readonly List<BpmnModelNs.ExtensionAttribute> DefaultElementAttributes = new()
    {
        new BpmnModelNs.ExtensionAttribute { Name = "id" },
        new BpmnModelNs.ExtensionAttribute { Name = "name" }
    };

    protected static readonly List<BpmnModelNs.ExtensionAttribute> DefaultActivityAttributes = new()
    {
        new BpmnModelNs.ExtensionAttribute { Namespace = BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE, Name = "async" },
        new BpmnModelNs.ExtensionAttribute { Namespace = BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE, Name = "exclusive" },
        new BpmnModelNs.ExtensionAttribute { Name = "default" },
        new BpmnModelNs.ExtensionAttribute { Namespace = BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE, Name = "isForCompensation" }
    };

    public void ConvertToBpmnModel(
        XmlNode xmlNode,
        BpmnModelNs.BpmnModel model,
        BpmnModelNs.Process activeProcess,
        List<BpmnModelNs.SubProcess> activeSubProcessList)
    {
        string? elementId = GetAttributeValue(xmlNode, "id");
        string? elementName = GetAttributeValue(xmlNode, "name");
        bool async = ParseAsync(xmlNode);
        bool notExclusive = ParseNotExclusive(xmlNode);
        string? defaultFlow = GetAttributeValue(xmlNode, "default");
        bool isForCompensation = ParseForCompensation(xmlNode);

        BpmnModelNs.BaseElement parsedElement = ConvertXMLToElement(xmlNode, model);

        if (parsedElement is BpmnModelNs.Artifact artifact)
            artifact.Id = elementId;

        if (parsedElement is BpmnModelNs.FlowElement currentFlowElement)
        {
            currentFlowElement.Id = elementId;
            currentFlowElement.Name = elementName;

            if (currentFlowElement is BpmnModelNs.FlowNode flowNode)
            {
                flowNode.Asynchronous = async;
                if (notExclusive)
                    flowNode.Exclusive = false;

                if (currentFlowElement is BpmnModelNs.Activity activity)
                {
                    activity.IsForCompensation = isForCompensation;
                    if (!string.IsNullOrEmpty(defaultFlow))
                        activity.DefaultFlow = defaultFlow;
                }

                if (currentFlowElement is BpmnModelNs.ExclusiveGateway exclGateway && !string.IsNullOrEmpty(defaultFlow))
                    exclGateway.DefaultFlow = defaultFlow;
                else if (currentFlowElement is BpmnModelNs.InclusiveGateway inclGateway && !string.IsNullOrEmpty(defaultFlow))
                    inclGateway.DefaultFlow = defaultFlow;
                else if (currentFlowElement is BpmnModelNs.ComplexGateway complGateway && !string.IsNullOrEmpty(defaultFlow))
                    complGateway.DefaultFlow = defaultFlow;
            }

            if (activeSubProcessList.Count > 0)
                activeSubProcessList[^1].FlowElements.Add(currentFlowElement);
            else
                activeProcess.FlowElements.Add(currentFlowElement);
        }
    }

    public void ConvertToXML(XmlWriter xtw, BpmnModelNs.BaseElement baseElement, BpmnModelNs.BpmnModel model)
    {
        xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, GetXMLElementName(), BpmnXMLConstants.BPMN2_NAMESPACE);

        bool didWriteExtensionStartElement = false;
        BpmnXMLUtil.WriteDefaultAttribute("id", baseElement.Id, xtw);

        if (baseElement is BpmnModelNs.FlowElement flowElement)
            BpmnXMLUtil.WriteDefaultAttribute("name", flowElement.Name, xtw);

        if (baseElement is BpmnModelNs.FlowNode flowNode)
        {
            if (flowNode.Asynchronous)
            {
                BpmnXMLUtil.WriteQualifiedAttribute("async", "true", xtw);
                if (!flowNode.Exclusive)
                    BpmnXMLUtil.WriteQualifiedAttribute("exclusive", "false", xtw);
            }

            if (baseElement is BpmnModelNs.Activity activity)
            {
                if (activity.IsForCompensation)
                    BpmnXMLUtil.WriteDefaultAttribute("isForCompensation", "true", xtw);
                if (!string.IsNullOrEmpty(activity.DefaultFlow))
                    BpmnXMLUtil.WriteDefaultAttribute("default", activity.DefaultFlow, xtw);
            }

            if (baseElement is BpmnModelNs.ExclusiveGateway exclGateway && !string.IsNullOrEmpty(exclGateway.DefaultFlow))
                BpmnXMLUtil.WriteDefaultAttribute("default", exclGateway.DefaultFlow, xtw);
            else if (baseElement is BpmnModelNs.InclusiveGateway inclGateway && !string.IsNullOrEmpty(inclGateway.DefaultFlow))
                BpmnXMLUtil.WriteDefaultAttribute("default", inclGateway.DefaultFlow, xtw);
            else if (baseElement is BpmnModelNs.ComplexGateway complGateway && !string.IsNullOrEmpty(complGateway.DefaultFlow))
                BpmnXMLUtil.WriteDefaultAttribute("default", complGateway.DefaultFlow, xtw);
        }

        WriteAdditionalAttributes(baseElement, model, xtw);

        if (baseElement is BpmnModelNs.FlowElement fe && !string.IsNullOrEmpty(fe.Documentation))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_DOCUMENTATION, BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteString(fe.Documentation);
            xtw.WriteEndElement();
        }

        didWriteExtensionStartElement = WriteExtensionChildElements(baseElement, didWriteExtensionStartElement, xtw);
        didWriteExtensionStartElement = WorkflowExtensionListenerExport.WriteListeners(baseElement, didWriteExtensionStartElement, xtw);
        didWriteExtensionStartElement = BpmnXMLUtil.WriteExtensionElements(baseElement, didWriteExtensionStartElement, xtw);

        if (baseElement is BpmnModelNs.Activity act)
            didWriteExtensionStartElement = FailedJobRetryCountExport.WriteFailedJobRetryCount(act, didWriteExtensionStartElement, xtw);

        if (didWriteExtensionStartElement)
            xtw.WriteEndElement();

        WriteIncomingOutgoingFlowElements(baseElement, xtw);

        if (baseElement is BpmnModelNs.Activity activityEl)
            MultiInstanceExport.WriteMultiInstance(activityEl, xtw);

        WriteAdditionalChildElements(baseElement, model, xtw);

        xtw.WriteEndElement();
    }

    public abstract Type GetBpmnElementType();
    protected abstract BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model);
    public abstract string GetXMLElementName();
    protected abstract void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw);
    protected abstract void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw);

    protected virtual bool WriteExtensionChildElements(BpmnModelNs.BaseElement element, bool didWriteExtensionStartElement, XmlWriter xtw)
    {
        return didWriteExtensionStartElement;
    }

    protected void ParseChildElements(string elementName, BpmnModelNs.BaseElement parentElement, XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        BpmnXMLUtil.ParseChildElements(elementName, parentElement, xmlNode, model);
    }

    protected void ParseChildElements(string elementName, BpmnModelNs.BaseElement parentElement, XmlNode xmlNode, Dictionary<string, BaseChildElementParser> additionalParsers, BpmnModelNs.BpmnModel model)
    {
        BpmnXMLUtil.ParseChildElements(elementName, parentElement, xmlNode, additionalParsers, model);
    }

    protected bool ParseAsync(XmlNode xmlNode)
    {
        var asyncString = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "async");
        return "true".Equals(asyncString, StringComparison.OrdinalIgnoreCase);
    }

    protected bool ParseNotExclusive(XmlNode xmlNode)
    {
        var exclusiveString = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "exclusive");
        return "false".Equals(exclusiveString, StringComparison.OrdinalIgnoreCase);
    }

    protected bool ParseForCompensation(XmlNode xmlNode)
    {
        var compensationString = GetAttributeValue(xmlNode, "isForCompensation");
        return "true".Equals(compensationString, StringComparison.OrdinalIgnoreCase);
    }

    protected List<string> ParseDelimitedList(string? expression) => BpmnXMLUtil.ParseDelimitedList(expression);
    protected string ConvertToDelimitedString(List<string> stringList) => BpmnXMLUtil.ConvertToDelimitedString(stringList);

    protected void WriteEventDefinitions(BpmnModelNs.Event parentEvent, List<BpmnModelNs.EventDefinition> eventDefinitions, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        foreach (var eventDefinition in eventDefinitions)
        {
            if (eventDefinition is BpmnModelNs.TimerEventDefinition timerDef)
                WriteTimerDefinition(parentEvent, timerDef, xtw);
            else if (eventDefinition is BpmnModelNs.SignalEventDefinition signalDef)
                WriteSignalDefinition(parentEvent, signalDef, xtw);
            else if (eventDefinition is BpmnModelNs.MessageEventDefinition messageDef)
                WriteMessageDefinition(parentEvent, messageDef, model, xtw);
            else if (eventDefinition is BpmnModelNs.LinkEventDefinition linkDef)
                WriteLinkDefinition(linkDef, xtw);
            else if (eventDefinition is BpmnModelNs.ErrorEventDefinition errorDef)
                WriteErrorDefinition(parentEvent, errorDef, xtw);
            else if (eventDefinition is BpmnModelNs.TerminateEventDefinition terminateDef)
                WriteTerminateDefinition(parentEvent, terminateDef, xtw);
            else if (eventDefinition is BpmnModelNs.CancelEventDefinition cancelDef)
                WriteCancelDefinition(parentEvent, cancelDef, xtw);
            else if (eventDefinition is BpmnModelNs.CompensateEventDefinition compensateDef)
                WriteCompensateDefinition(parentEvent, compensateDef, xtw);
            else if (eventDefinition is BpmnModelNs.EscalationEventDefinition escalationDef)
                WriteEscalationDefinition(parentEvent, escalationDef, xtw);
            else if (eventDefinition is BpmnModelNs.ConditionalEventDefinition conditionalDef)
                WriteConditionalDefinition(parentEvent, conditionalDef, xtw);
            else
                throw new WorkflowEngineException(
                    $"Unsupported event definition '{eventDefinition.GetType().Name}' in event '{parentEvent.Id ?? "<unknown>"}'.");
        }
    }

    protected void WriteTimerDefinition(BpmnModelNs.Event parentEvent, BpmnModelNs.TimerEventDefinition timerDefinition, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_TIMERDEFINITION);
        if (!string.IsNullOrEmpty(timerDefinition.CalendarName))
            BpmnXMLUtil.WriteQualifiedAttribute(BpmnXMLConstants.ATTRIBUTE_CALENDAR_NAME, timerDefinition.CalendarName, xtw);
        if (!string.IsNullOrEmpty(timerDefinition.TimeDate))
        {
            xtw.WriteStartElement(BpmnXMLConstants.ATTRIBUTE_TIMER_DATE);
            xtw.WriteString(timerDefinition.TimeDate);
            xtw.WriteEndElement();
        }
        else if (!string.IsNullOrEmpty(timerDefinition.TimeCycle))
        {
            xtw.WriteStartElement(BpmnXMLConstants.ATTRIBUTE_TIMER_CYCLE);
            if (!string.IsNullOrEmpty(timerDefinition.EndDate))
                xtw.WriteAttributeString(BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, BpmnXMLConstants.ATTRIBUTE_END_DATE, BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE, timerDefinition.EndDate);
            xtw.WriteString(timerDefinition.TimeCycle);
            xtw.WriteEndElement();
        }
        else if (!string.IsNullOrEmpty(timerDefinition.TimeDuration))
        {
            xtw.WriteStartElement(BpmnXMLConstants.ATTRIBUTE_TIMER_DURATION);
            xtw.WriteString(timerDefinition.TimeDuration);
            xtw.WriteEndElement();
        }
        xtw.WriteEndElement();
    }

    protected void WriteSignalDefinition(BpmnModelNs.Event parentEvent, BpmnModelNs.SignalEventDefinition signalDefinition, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_SIGNALDEFINITION);
        BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_SIGNAL_REF, signalDefinition.SignalRef, xtw);
        xtw.WriteEndElement();
    }

    protected void WriteMessageDefinition(BpmnModelNs.Event parentEvent, BpmnModelNs.MessageEventDefinition messageDefinition, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_MESSAGEDEFINITION);
        BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_MESSAGE_REF, messageDefinition.MessageRef, xtw);
        xtw.WriteEndElement();
    }

    protected void WriteErrorDefinition(BpmnModelNs.Event parentEvent, BpmnModelNs.ErrorEventDefinition errorDefinition, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_ERRORDEFINITION);
        BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_ERROR_REF, errorDefinition.ErrorCode, xtw);
        xtw.WriteEndElement();
    }

    protected void WriteCancelDefinition(BpmnModelNs.Event parentEvent, BpmnModelNs.CancelEventDefinition cancelEventDefinition, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_CANCELDEFINITION);
        xtw.WriteEndElement();
    }

    protected void WriteCompensateDefinition(BpmnModelNs.Event parentEvent, BpmnModelNs.CompensateEventDefinition compensateEventDefinition, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_COMPENSATEDEFINITION);
        BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_COMPENSATE_ACTIVITYREF, compensateEventDefinition.ActivityRef, xtw);
        xtw.WriteEndElement();
    }

    protected void WriteTerminateDefinition(BpmnModelNs.Event parentEvent, BpmnModelNs.TerminateEventDefinition terminateDefinition, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_TERMINATEDEFINITION);
        if (terminateDefinition.TerminateAll)
            BpmnXMLUtil.WriteQualifiedAttribute(BpmnXMLConstants.ATTRIBUTE_TERMINATE_ALL, "true", xtw);
        xtw.WriteEndElement();
    }

    protected void WriteEscalationDefinition(BpmnModelNs.Event parentEvent, BpmnModelNs.EscalationEventDefinition escalationDefinition, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_ESCALATIONDEFINITION);
        BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_ESCALATION_REF, escalationDefinition.EscalationRef, xtw);
        if (!string.IsNullOrWhiteSpace(escalationDefinition.EscalationCode))
            BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_ESCALATION_CODE, escalationDefinition.EscalationCode, xtw);
        xtw.WriteEndElement();
    }

    protected void WriteConditionalDefinition(BpmnModelNs.Event parentEvent, BpmnModelNs.ConditionalEventDefinition conditionalDefinition, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_CONDITIONALDEFINITION);
        var condition = conditionalDefinition.ConditionExpression ?? conditionalDefinition.Condition;
        if (!string.IsNullOrWhiteSpace(condition))
        {
            xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_CONDITION);
            xtw.WriteString(condition);
            xtw.WriteEndElement();
        }
        xtw.WriteEndElement();
    }

    protected void WriteLinkDefinition(BpmnModelNs.LinkEventDefinition linkDefinition, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EVENT_LINKDEFINITION);
        BpmnXMLUtil.WriteDefaultAttribute("name", linkDefinition.Name, xtw);
        foreach (var source in linkDefinition.Sources)
        {
            xtw.WriteStartElement("source");
            xtw.WriteString(source);
            xtw.WriteEndElement();
        }
        if (!string.IsNullOrEmpty(linkDefinition.Target))
        {
            xtw.WriteStartElement("target");
            xtw.WriteString(linkDefinition.Target);
            xtw.WriteEndElement();
        }
        xtw.WriteEndElement();
    }

    protected void WriteIncomingOutgoingFlowElements(BpmnModelNs.BaseElement element, XmlWriter xtw)
    {
        if (element is BpmnModelNs.FlowNode flowNode)
            BpmnXMLUtil.WriteIncomingAndOutgoingFlowElement(flowNode, xtw);
    }

    protected static string? GetAttributeValue(XmlNode node, string localName) => node.Attributes?[localName]?.Value;

    protected static string? GetAttributeValue(XmlNode node, string prefix, string localName)
    {
        if (node.Attributes == null) return null;
        foreach (XmlAttribute attr in node.Attributes)
        {
            if (attr.LocalName == localName && attr.Prefix == prefix)
                return attr.Value;
        }
        return null;
    }
}


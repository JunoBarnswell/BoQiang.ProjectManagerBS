using System;
using System.Collections.Generic;
using System.Xml;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public abstract class BaseChildElementParser
{
    public abstract string ElementName { get; }
    public abstract void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model);

    public virtual bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element != null;
    }

    protected void ParseChildElementsForParser(
        XmlNode parentNode,
        BpmnModelNs.BaseElement parentElement,
        BpmnModelNs.BpmnModel model,
        BaseChildElementParser parser)
    {
        foreach (XmlNode child in parentNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == parser.ElementName)
            {
                parser.ParseChildElement(child, parentElement, model);
            }
        }
    }

    protected static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }

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

    protected static string? GetAttributeValueAnyNs(XmlNode node, string localName)
    {
        if (node.Attributes == null) return null;
        foreach (XmlAttribute attr in node.Attributes)
        {
            if (attr.LocalName == localName)
                return attr.Value;
        }
        return null;
    }
}

public class TimerEventDefinitionParser : BaseChildElementParser
{
    public override string ElementName => "timerEventDefinition";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var timerDef = new BpmnModelNs.TimerEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            switch (child.LocalName)
            {
                case "timeDate": timerDef.TimeDate = child.InnerText; break;
                case "timeCycle":
                    timerDef.TimeCycle = child.InnerText;
                    timerDef.EndDate = GetAttributeValueAnyNs(child, "endDate")
                                       ?? GetAttributeValue(child, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "endDate");
                    break;
                case "timeDuration": timerDef.TimeDuration = child.InnerText; break;
            }
        }

        timerDef.EndDate ??= GetAttributeValueAnyNs(xmlNode, "endDate")
                          ?? GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "endDate");
        timerDef.CalendarName = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "calendarName");

        AddEventDefinition(parentElement, timerDef);
    }

    private static void AddEventDefinition(BpmnModelNs.BaseElement parentElement, BpmnModelNs.EventDefinition def)
    {
        if (parentElement is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (parentElement is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }
}

public class TimeDateParser : BaseChildElementParser
{
    public override string ElementName => "timeDate";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.TimerEventDefinition timerDef)
            timerDef.TimeDate = xmlNode.InnerText;
    }
}

public class TimeDurationParser : BaseChildElementParser
{
    public override string ElementName => "timeDuration";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.TimerEventDefinition timerDef)
            timerDef.TimeDuration = xmlNode.InnerText;
    }
}

public class TimeCycleParser : BaseChildElementParser
{
    public override string ElementName => "timeCycle";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.TimerEventDefinition timerDef)
        {
            timerDef.TimeCycle = xmlNode.InnerText;
            var endDate = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "endDate");
            if (!string.IsNullOrEmpty(endDate))
                timerDef.EndDate = endDate;
        }
    }
}

public class SignalEventDefinitionParser : BaseChildElementParser
{
    public override string ElementName => "signalEventDefinition";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var signalDef = new BpmnModelNs.SignalEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            SignalRef = GetAttributeValue(xmlNode, "signalRef")
        };

        var scope = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "scope");
        if (!string.IsNullOrEmpty(scope))
            signalDef.Scope = scope;

        AddEventDefinition(parentElement, signalDef);
    }

    private static void AddEventDefinition(BpmnModelNs.BaseElement parentElement, BpmnModelNs.EventDefinition def)
    {
        if (parentElement is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (parentElement is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }
}

public class MessageEventDefinitionParser : BaseChildElementParser
{
    public override string ElementName => "messageEventDefinition";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var messageDef = new BpmnModelNs.MessageEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            MessageRef = GetAttributeValue(xmlNode, "messageRef")
        };

        AddEventDefinition(parentElement, messageDef);
    }

    private static void AddEventDefinition(BpmnModelNs.BaseElement parentElement, BpmnModelNs.EventDefinition def)
    {
        if (parentElement is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (parentElement is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }
}

public class ErrorEventDefinitionParser : BaseChildElementParser
{
    public override string ElementName => "errorEventDefinition";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var errorDef = new BpmnModelNs.ErrorEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            ErrorCode = GetAttributeValue(xmlNode, "errorCode") ?? GetAttributeValue(xmlNode, "errorRef"),
            ErrorHandlerId = GetAttributeValue(xmlNode, "errorRef")
        };

        AddEventDefinition(parentElement, errorDef);
    }

    private static void AddEventDefinition(BpmnModelNs.BaseElement parentElement, BpmnModelNs.EventDefinition def)
    {
        if (parentElement is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (parentElement is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }
}

public class CompensateEventDefinitionParser : BaseChildElementParser
{
    public override string ElementName => "compensateEventDefinition";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var compensateDef = new BpmnModelNs.CompensateEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            ActivityRef = GetAttributeValue(xmlNode, "activityRef")
        };

        var waitForCompletion = GetAttributeValueAnyNs(xmlNode, "waitForCompletion")
                                ?? GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "waitForCompletion");
        if (!string.IsNullOrEmpty(waitForCompletion))
            compensateDef.WaitForCompletion = waitForCompletion.ToLowerInvariant() == "true";

        AddEventDefinition(parentElement, compensateDef);
    }

    private static void AddEventDefinition(BpmnModelNs.BaseElement parentElement, BpmnModelNs.EventDefinition def)
    {
        if (parentElement is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (parentElement is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }
}

public class CancelEventDefinitionParser : BaseChildElementParser
{
    public override string ElementName => "cancelEventDefinition";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var cancelDef = new BpmnModelNs.CancelEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id")
        };

        AddEventDefinition(parentElement, cancelDef);
    }

    private static void AddEventDefinition(BpmnModelNs.BaseElement parentElement, BpmnModelNs.EventDefinition def)
    {
        if (parentElement is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (parentElement is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }
}

public class TerminateEventDefinitionParser : BaseChildElementParser
{
    public override string ElementName => "terminateEventDefinition";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var terminateDef = new BpmnModelNs.TerminateEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id")
        };

        var terminateAll = GetAttributeValueAnyNs(xmlNode, "terminateAll")
                           ?? GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "terminateAll");
        if (!string.IsNullOrEmpty(terminateAll))
            terminateDef.TerminateAll = terminateAll.ToLowerInvariant() == "true";

        AddEventDefinition(parentElement, terminateDef);
    }

    private static void AddEventDefinition(BpmnModelNs.BaseElement parentElement, BpmnModelNs.EventDefinition def)
    {
        if (parentElement is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (parentElement is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }
}

public class LinkEventDefinitionParser : BaseChildElementParser
{
    public override string ElementName => "linkEventDefinition";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var linkDef = new BpmnModelNs.LinkEventDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "source")
                linkDef.Sources.Add(child.InnerText);
            else if (child.LocalName == "target")
                linkDef.Target = child.InnerText;
        }

        AddEventDefinition(parentElement, linkDef);
    }

    private static void AddEventDefinition(BpmnModelNs.BaseElement parentElement, BpmnModelNs.EventDefinition def)
    {
        if (parentElement is BpmnModelNs.CatchEvent catchEvent)
            catchEvent.EventDefinitions.Add(def);
        else if (parentElement is BpmnModelNs.ThrowEvent throwEvent)
            throwEvent.EventDefinitions.Add(def);
    }
}

public class LinkEventSourceParser : BaseChildElementParser
{
    public override string ElementName => "source";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.LinkEventDefinition;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.LinkEventDefinition linkDef)
            linkDef.Sources.Add(xmlNode.InnerText);
    }
}

public class LinkEventTargetParser : BaseChildElementParser
{
    public override string ElementName => "target";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.LinkEventDefinition;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.LinkEventDefinition linkDef)
            linkDef.Target = xmlNode.InnerText;
    }
}

public class IOSpecificationParser : BaseChildElementParser
{
    public override string ElementName => "ioSpecification";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var ioSpec = new BpmnModelNs.IOSpecification();

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            switch (child.LocalName)
            {
                case "dataInput":
                    ioSpec.DataInputs.Add(new BpmnModelNs.DataInput
                    {
                        Id = GetAttributeValue(child, "id"),
                        Name = GetAttributeValue(child, "name"),
                        ItemSubjectRef = GetAttributeValue(child, "itemSubjectRef")
                    });
                    break;
                case "dataOutput":
                    ioSpec.DataOutputs.Add(new BpmnModelNs.DataOutput
                    {
                        Id = GetAttributeValue(child, "id"),
                        Name = GetAttributeValue(child, "name"),
                        ItemSubjectRef = GetAttributeValue(child, "itemSubjectRef")
                    });
                    break;
                case "inputSet":
                    foreach (XmlNode inputSetChild in child.ChildNodes)
                    {
                        if (inputSetChild.NodeType != XmlNodeType.Element) continue;
                        if (inputSetChild.LocalName == "dataInputRefs" &&
                            !string.IsNullOrWhiteSpace(inputSetChild.InnerText))
                        {
                            ioSpec.DataInputRefs.Add(inputSetChild.InnerText.Trim());
                        }
                    }
                    break;
                case "outputSet":
                    foreach (XmlNode outputSetChild in child.ChildNodes)
                    {
                        if (outputSetChild.NodeType != XmlNodeType.Element) continue;
                        if (outputSetChild.LocalName == "dataOutputRefs" &&
                            !string.IsNullOrWhiteSpace(outputSetChild.InnerText))
                        {
                            ioSpec.DataOutputRefs.Add(outputSetChild.InnerText.Trim());
                        }
                    }
                    break;
            }
        }

        if (parentElement is BpmnModelNs.CallActivity callActivity)
            callActivity.IOSpecification.Add(ioSpec);
        else if (parentElement is BpmnModelNs.ServiceTask serviceTask)
            serviceTask.IOSpecification.Add(ioSpec);
        else if (parentElement is BpmnModelNs.Activity activity)
        {
        }
    }
}

public class FormPropertyParser : BaseChildElementParser
{
    public override string ElementName => "formProperty";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.UserTask || element is BpmnModelNs.StartEvent;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var formProperty = new BpmnModelNs.FormProperty
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            Type = GetAttributeValue(xmlNode, "type"),
            Expression = GetAttributeValue(xmlNode, "expression"),
            Variable = GetAttributeValue(xmlNode, "variable"),
            DefaultExpression = GetAttributeValue(xmlNode, "default"),
            DatePattern = GetAttributeValue(xmlNode, "datePattern")
        };

        var readable = GetAttributeValue(xmlNode, "readable");
        if (!string.IsNullOrEmpty(readable))
            formProperty.Readable = readable.ToLowerInvariant() == "true";
        else
            formProperty.Readable = true;

        var writable = GetAttributeValue(xmlNode, "writable");
        if (!string.IsNullOrEmpty(writable))
            formProperty.Writable = writable.ToLowerInvariant() == "true";
        else
            formProperty.Writable = true;

        var required = GetAttributeValue(xmlNode, "required");
        if (!string.IsNullOrEmpty(required))
            formProperty.Required = required.ToLowerInvariant() == "true";

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "value" || child.LocalName == "formValue")
            {
                formProperty.Values.Add(new BpmnModelNs.FormValue
                {
                    Id = GetAttributeValue(child, "id"),
                    Name = GetAttributeValue(child, "name")
                });
            }
        }

        if (parentElement is BpmnModelNs.UserTask userTask)
            userTask.FormProperties.Add(formProperty);
        else if (parentElement is BpmnModelNs.StartEvent startEvent)
            startEvent.FormProperties.Add(formProperty);
    }
}

public class FieldExtensionParser : BaseChildElementParser
{
    public override string ElementName => "field";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.ServiceTask || element is BpmnModelNs.WorkflowExtensionListener;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var fieldExtension = new BpmnModelNs.FieldExtension
        {
            FieldName = GetAttributeValue(xmlNode, "fieldName") ?? GetAttributeValue(xmlNode, "name")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "string")
                fieldExtension.StringValue = child.InnerText;
            else if (child.LocalName == "expression")
                fieldExtension.Expression = child.InnerText;
        }

        if (parentElement is BpmnModelNs.ServiceTask serviceTask)
            serviceTask.FieldExtensions.Add(fieldExtension);
        else if (parentElement is BpmnModelNs.WorkflowExtensionListener listener)
            listener.FieldExtensions.Add(fieldExtension);
    }
}

public class ExecutionListenerParser : BaseChildElementParser
{
    public override string ElementName => "executionListener";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var listener = new BpmnModelNs.WorkflowExtensionListener
        {
            Event = GetAttributeValue(xmlNode, "event"),
            ImplementationType = DetermineImplementationType(xmlNode),
            Implementation = GetAttributeValue(xmlNode, "class") ?? GetAttributeValue(xmlNode, "expression") ?? GetAttributeValue(xmlNode, "delegateExpression")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "field")
            {
                var fieldParser = new FieldExtensionParser();
                fieldParser.ParseChildElement(child, listener, model);
            }
        }

        if (parentElement is BpmnModelNs.FlowElement flowElement)
            flowElement.ExecutionListeners.Add(listener);
        else if (parentElement is BpmnModelNs.Process process)
            process.ExecutionListeners.Add(listener);
    }

    private static string? DetermineImplementationType(XmlNode node)
    {
        if (GetAttributeValue(node, "class") != null) return "class";
        if (GetAttributeValue(node, "expression") != null) return "expression";
        if (GetAttributeValue(node, "delegateExpression") != null) return "delegateExpression";
        return null;
    }
}

public class InParameterParser : BaseChildElementParser
{
    public override string ElementName => "in";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.CallActivity;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is not BpmnModelNs.CallActivity callActivity)
            return;

        callActivity.InParameters.Add(new BpmnModelNs.IOParameter
        {
            Source = GetAttributeValue(xmlNode, "source"),
            SourceExpression = GetAttributeValue(xmlNode, "sourceExpression"),
            Target = GetAttributeValue(xmlNode, "target"),
            TargetExpression = GetAttributeValue(xmlNode, "targetExpression")
        });
    }
}

public class OutParameterParser : BaseChildElementParser
{
    public override string ElementName => "out";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.CallActivity;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is not BpmnModelNs.CallActivity callActivity)
            return;

        callActivity.OutParameters.Add(new BpmnModelNs.IOParameter
        {
            Source = GetAttributeValue(xmlNode, "source"),
            SourceExpression = GetAttributeValue(xmlNode, "sourceExpression"),
            Target = GetAttributeValue(xmlNode, "target"),
            TargetExpression = GetAttributeValue(xmlNode, "targetExpression")
        });
    }
}

public class TaskListenerParser : BaseChildElementParser
{
    public override string ElementName => "taskListener";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.UserTask;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var listener = new BpmnModelNs.WorkflowExtensionListener
        {
            Event = GetAttributeValue(xmlNode, "event"),
            ImplementationType = DetermineImplementationType(xmlNode),
            Implementation = GetAttributeValue(xmlNode, "class") ?? GetAttributeValue(xmlNode, "expression") ?? GetAttributeValue(xmlNode, "delegateExpression")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "field")
            {
                var fieldParser = new FieldExtensionParser();
                fieldParser.ParseChildElement(child, listener, model);
            }
        }

        if (parentElement is BpmnModelNs.UserTask userTask)
            userTask.TaskListeners.Add(listener);
    }

    private static string? DetermineImplementationType(XmlNode node)
    {
        if (GetAttributeValue(node, "class") != null) return "class";
        if (GetAttributeValue(node, "expression") != null) return "expression";
        if (GetAttributeValue(node, "delegateExpression") != null) return "delegateExpression";
        return null;
    }
}

public class WorkflowExtensionListenerParser : BaseChildElementParser
{
    public override string ElementName => "listener";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var listener = new BpmnModelNs.WorkflowExtensionListener
        {
            Event = GetAttributeValue(xmlNode, "event"),
            ImplementationType = DetermineImplementationType(xmlNode),
            Implementation = GetAttributeValue(xmlNode, "class") ?? GetAttributeValue(xmlNode, "expression") ?? GetAttributeValue(xmlNode, "delegateExpression")
        };

        if (parentElement is BpmnModelNs.FlowElement flowElement)
            flowElement.ExecutionListeners.Add(listener);
        else if (parentElement is BpmnModelNs.Process process)
            process.ExecutionListeners.Add(listener);
    }

    private static string? DetermineImplementationType(XmlNode node)
    {
        if (GetAttributeValue(node, "class") != null) return "class";
        if (GetAttributeValue(node, "expression") != null) return "expression";
        if (GetAttributeValue(node, "delegateExpression") != null) return "delegateExpression";
        return null;
    }
}

public class WorkflowEventListenerParser : BaseChildElementParser
{
    public override string ElementName => "eventListener";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.Process;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is not BpmnModelNs.Process)
            return;

        var extensionElement = BpmnXMLUtil.ParseExtensionElement(xmlNode);
        parentElement.AddExtensionElement(extensionElement);
    }

    private static string? DetermineImplementationType(XmlNode node)
    {
        if (GetAttributeValue(node, "class") != null) return "class";
        if (GetAttributeValue(node, "delegateExpression") != null) return "delegateExpression";
        if (GetAttributeValue(node, "throwEvent") != null) return "throwEvent";
        return null;
    }
}

public class WorkflowFailedJobRetryParser : BaseChildElementParser
{
    public override string ElementName => "failedJobRetryTimeCycle";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.Activity;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var retryCycle = xmlNode.InnerText?.Trim();
        if (string.IsNullOrWhiteSpace(retryCycle))
            return;

        parentElement.AddAttribute(new BpmnModelNs.ExtensionAttribute
        {
            Namespace = BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE,
            NamespacePrefix = BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX,
            Name = BpmnXMLConstants.FAILED_JOB_RETRY_TIME_CYCLE,
            Value = retryCycle
        });
    }
}

public class WorkflowMapExceptionParser : BaseChildElementParser
{
    public override string ElementName => "mapException";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.ServiceTask || element is BpmnModelNs.CallActivity;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var entry = new BpmnModelNs.MapExceptionEntry
        {
            ClassName = GetAttributeValue(xmlNode, "errorClass"),
            ErrorCode = GetAttributeValue(xmlNode, "errorCode"),
            AndChildren = GetAttributeValue(xmlNode, "andChildren")?.ToLowerInvariant() == "true"
        };

        if (parentElement is BpmnModelNs.ServiceTask serviceTask)
            serviceTask.MapExceptions.Add(entry);
        else if (parentElement is BpmnModelNs.CallActivity callActivity)
            callActivity.MapExceptions.Add(entry);
    }
}

public class ConditionExpressionParser : BaseChildElementParser
{
    public override string ElementName => "conditionExpression";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.SequenceFlow;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.SequenceFlow sequenceFlow)
            sequenceFlow.ConditionExpression = xmlNode.InnerText;
    }
}

public class DataInputAssociationParser : BaseChildElementParser
{
    public override string ElementName => "dataInputAssociation";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var association = ParseAssociation(xmlNode, true);
        if (association == null)
            return;
        AddAssociation(parentElement, association);
    }

    internal static BpmnModelNs.InputOutputAssociation? ParseAssociation(XmlNode xmlNode, bool isInputAssociation)
    {
        var sourceRef = string.Empty;
        string? targetRef = null;
        string? transformation = null;
        var assignments = new List<BpmnModelNs.Assignment>();

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "sourceRef")
                sourceRef = child.InnerText;
            else if (child.LocalName == "targetRef")
                targetRef = child.InnerText;
            else if (child.LocalName == "transformation")
                transformation = child.InnerText;
            else if (child.LocalName == "assignment")
            {
                string? from = null;
                string? to = null;
                foreach (XmlNode assignmentChild in child.ChildNodes)
                {
                    if (assignmentChild.NodeType != XmlNodeType.Element) continue;
                    if (assignmentChild.LocalName == "from")
                        from = assignmentChild.InnerText;
                    else if (assignmentChild.LocalName == "to")
                        to = assignmentChild.InnerText;
                }

                if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
                {
                    assignments.Add(new BpmnModelNs.Assignment
                    {
                        Id = GetAttributeValue(child, "id"),
                        From = from,
                        To = to
                    });
                }
            }
        }

        if (string.IsNullOrWhiteSpace(sourceRef) &&
            string.IsNullOrWhiteSpace(targetRef) &&
            string.IsNullOrWhiteSpace(transformation) &&
            assignments.Count == 0)
        {
            return null;
        }

        var association = new BpmnModelNs.InputOutputAssociation
        {
            Id = GetAttributeValue(xmlNode, "id"),
            IsInputAssociation = isInputAssociation,
            SourceRef = sourceRef,
            TargetRef = targetRef,
            Transformation = transformation
        };
        association.Assignments.AddRange(assignments);
        return association;
    }

    internal static void AddAssociation(BpmnModelNs.BaseElement parentElement, BpmnModelNs.InputOutputAssociation association)
    {
        if (parentElement is BpmnModelNs.CallActivity callActivity)
        {
            if (callActivity.IOSpecification.Count == 0)
                callActivity.IOSpecification.Add(new BpmnModelNs.IOSpecification());
            callActivity.IOSpecification[0].InputOutputAssociations.Add(association);
            return;
        }

        if (parentElement is BpmnModelNs.ServiceTask serviceTask)
        {
            if (serviceTask.IOSpecification.Count == 0)
                serviceTask.IOSpecification.Add(new BpmnModelNs.IOSpecification());
            serviceTask.IOSpecification[0].InputOutputAssociations.Add(association);
        }
    }
}

public class DataOutputAssociationParser : BaseChildElementParser
{
    public override string ElementName => "dataOutputAssociation";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var association = DataInputAssociationParser.ParseAssociation(xmlNode, false);
        if (association == null)
            return;
        DataInputAssociationParser.AddAssociation(parentElement, association);
    }
}

public class DataStateParser : BaseChildElementParser
{
    public override string ElementName => "dataState";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var stateValue = xmlNode.InnerText?.Trim();
        if (string.IsNullOrWhiteSpace(stateValue))
            return;

        var extension = new BpmnModelNs.ExtensionElement
        {
            Name = "dataState",
            Namespace = xmlNode.NamespaceURI,
            NamespacePrefix = xmlNode.Prefix,
            ElementText = stateValue
        };
        parentElement.AddExtensionElement(extension);
    }
}

public class DocumentationParser : BaseChildElementParser
{
    public override string ElementName => "documentation";

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.FlowElement flowElement)
            flowElement.Documentation = xmlNode.InnerText;
        else if (parentElement is BpmnModelNs.Process process)
            process.Documentation = xmlNode.InnerText;
    }
}

public class ScriptTextParser : BaseChildElementParser
{
    public override string ElementName => "script";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.ScriptTask;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.ScriptTask scriptTask)
            scriptTask.Script = xmlNode.InnerText;
    }
}

public class FlowNodeRefParser : BaseChildElementParser
{
    public override string ElementName => "flowNodeRef";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.Lane;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.Lane lane)
            lane.FlowReferences.Add(xmlNode.InnerText);
    }
}

public class ElementParser : BaseChildElementParser
{
    private readonly string _elementName;

    public ElementParser(string elementName)
    {
        _elementName = elementName;
    }

    public override string ElementName => _elementName;

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var extensionElement = BpmnXMLUtil.ParseExtensionElement(xmlNode);
        parentElement.AddExtensionElement(extensionElement);
    }
}

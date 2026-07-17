using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public static class BpmnXMLUtil
{
    private static readonly Dictionary<string, BaseChildElementParser> _genericChildParserMap = new();

    static BpmnXMLUtil()
    {
        AddGenericParser(new WorkflowEventListenerParser());
        AddGenericParser(new CancelEventDefinitionParser());
        AddGenericParser(new CompensateEventDefinitionParser());
        AddGenericParser(new ConditionExpressionParser());
        AddGenericParser(new ScriptTextParser());
        AddGenericParser(new DataInputAssociationParser());
        AddGenericParser(new DataOutputAssociationParser());
        AddGenericParser(new DataStateParser());
        AddGenericParser(new DocumentationParser());
        AddGenericParser(new ErrorEventDefinitionParser());
        AddGenericParser(new EscalationEventDefinitionParser());
        AddGenericParser(new ExecutionListenerParser());
        AddGenericParser(new FieldExtensionParser());
        AddGenericParser(new FormPropertyParser());
        AddGenericParser(new IOSpecificationParser());
        AddGenericParser(new MessageEventDefinitionParser());
        AddGenericParser(new MultiInstanceParser());
        AddGenericParser(new ConditionalEventDefinitionParser());
        AddGenericParser(new SignalEventDefinitionParser());
        AddGenericParser(new TaskListenerParser());
        AddGenericParser(new TerminateEventDefinitionParser());
        AddGenericParser(new TimerEventDefinitionParser());
        AddGenericParser(new TimeDateParser());
        AddGenericParser(new TimeCycleParser());
        AddGenericParser(new TimeDurationParser());
        AddGenericParser(new FlowNodeRefParser());
        AddGenericParser(new WorkflowFailedJobRetryParser());
        AddGenericParser(new WorkflowMapExceptionParser());
        AddGenericParser(new LinkEventDefinitionParser());
        AddGenericParser(new LinkEventTargetParser());
        AddGenericParser(new LinkEventSourceParser());
        AddGenericParser(new InParameterParser());
        AddGenericParser(new OutParameterParser());
    }

    private static void AddGenericParser(BaseChildElementParser parser)
    {
        _genericChildParserMap[parser.ElementName] = parser;
    }

    public static void ParseChildElements(
        string elementName,
        BpmnModelNs.BaseElement parentElement,
        XmlNode parentNode,
        BpmnModelNs.BpmnModel model)
    {
        ParseChildElements(elementName, parentElement, parentNode, null, model);
    }

    public static void ParseChildElements(
        string elementName,
        BpmnModelNs.BaseElement parentElement,
        XmlNode parentNode,
        Dictionary<string, BaseChildElementParser>? childParsers,
        BpmnModelNs.BpmnModel model)
    {
        var localParserMap = new Dictionary<string, BaseChildElementParser>(_genericChildParserMap);
        if (childParsers != null)
        {
            foreach (var kvp in childParsers)
                localParserMap[kvp.Key] = kvp.Value;
        }

        bool inExtensionElements = false;
        foreach (XmlNode child in parentNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;

            if (child.LocalName == BpmnXMLConstants.ELEMENT_EXTENSIONS)
            {
                inExtensionElements = true;
                ParseExtensionChildElements(child, parentElement, localParserMap, model);
                inExtensionElements = false;
                continue;
            }

            if (localParserMap.TryGetValue(child.LocalName, out var parser))
            {
                if (inExtensionElements && !parser.Accepts(parentElement))
                {
                    var extensionElement = ParseExtensionElement(child);
                    parentElement.AddExtensionElement(extensionElement);
                    continue;
                }
                parser.ParseChildElement(child, parentElement, model);
            }
            else if (inExtensionElements)
            {
                var extensionElement = ParseExtensionElement(child);
                parentElement.AddExtensionElement(extensionElement);
            }
        }
    }

    public static BpmnModelNs.ExtensionElement ParseExtensionElement(XmlNode node)
    {
        var extensionElement = new BpmnModelNs.ExtensionElement
        {
            Name = node.LocalName,
            Namespace = node.NamespaceURI,
            NamespacePrefix = node.Prefix
        };

        AddCustomAttributes(node, extensionElement);

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType == XmlNodeType.Text || child.NodeType == XmlNodeType.CDATA)
            {
                var text = child.Value?.Trim();
                if (!string.IsNullOrEmpty(text))
                    extensionElement.ElementText = text;
            }
            else if (child.NodeType == XmlNodeType.Element)
            {
                var childExtensionElement = ParseExtensionElement(child);
                extensionElement.ChildElements.Add(childExtensionElement);
            }
        }

        return extensionElement;
    }

    public static void AddCustomAttributes(XmlNode node, BpmnModelNs.BaseElement element)
    {
        if (node.Attributes == null) return;
        foreach (XmlAttribute attr in node.Attributes)
        {
            var extensionAttribute = new BpmnModelNs.ExtensionAttribute
            {
                Name = attr.LocalName,
                Value = attr.Value,
                Namespace = attr.NamespaceURI,
                NamespacePrefix = attr.Prefix
            };
            element.AddAttribute(extensionAttribute);
        }
    }

    public static void WriteDefaultAttribute(string attributeName, string? value, XmlWriter xtw)
    {
        if (!string.IsNullOrEmpty(value) && value != "null")
            xtw.WriteAttributeString(attributeName, value);
    }

    public static void WriteQualifiedAttribute(string attributeName, string? value, XmlWriter xtw)
    {
        if (!string.IsNullOrEmpty(value))
            xtw.WriteAttributeString(BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, attributeName, BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE, value);
    }

    public static bool WriteExtensionElements(
        BpmnModelNs.BaseElement baseElement,
        bool didWriteExtensionStartElement,
        XmlWriter xtw)
    {
        return WriteExtensionElements(baseElement, didWriteExtensionStartElement, null, xtw);
    }

    public static bool WriteExtensionElements(
        BpmnModelNs.BaseElement baseElement,
        bool didWriteExtensionStartElement,
        Dictionary<string, string>? namespaceMap,
        XmlWriter xtw)
    {
        if (baseElement.ExtensionElements.Count == 0)
            return didWriteExtensionStartElement;

        if (!didWriteExtensionStartElement)
        {
            xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EXTENSIONS);
            didWriteExtensionStartElement = true;
        }

        namespaceMap ??= new Dictionary<string, string>();

        foreach (var kvp in baseElement.ExtensionElements)
        {
            foreach (var extensionElement in kvp.Value)
            {
                WriteExtensionElement(extensionElement, namespaceMap, xtw);
            }
        }

        return didWriteExtensionStartElement;
    }

    public static void WriteExtensionElement(
        BpmnModelNs.ExtensionElement extensionElement,
        Dictionary<string, string> namespaceMap,
        XmlWriter xtw)
    {
        if (string.IsNullOrEmpty(extensionElement.Name)) return;

        var localNamespaceMap = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(extensionElement.Namespace))
        {
            if (!string.IsNullOrEmpty(extensionElement.NamespacePrefix))
            {
                xtw.WriteStartElement(extensionElement.NamespacePrefix, extensionElement.Name, extensionElement.Namespace);

                if (!namespaceMap.ContainsKey(extensionElement.NamespacePrefix) ||
                    namespaceMap[extensionElement.NamespacePrefix] != extensionElement.Namespace)
                {
                    xtw.WriteAttributeString("xmlns", extensionElement.NamespacePrefix, null, extensionElement.Namespace);
                    namespaceMap[extensionElement.NamespacePrefix] = extensionElement.Namespace;
                    localNamespaceMap[extensionElement.NamespacePrefix] = extensionElement.Namespace;
                }
            }
            else
            {
                xtw.WriteStartElement(extensionElement.Namespace, extensionElement.Name);
            }
        }
        else
        {
            xtw.WriteStartElement(extensionElement.Name);
        }

        foreach (var attrList in extensionElement.Attributes)
        {
            if (attrList is BpmnModelNs.ExtensionAttribute attribute && !string.IsNullOrEmpty(attribute.Name) && attribute.Value != null)
            {
                if (!string.IsNullOrEmpty(attribute.Namespace))
                {
                    if (!string.IsNullOrEmpty(attribute.NamespacePrefix))
                    {
                        if (!namespaceMap.ContainsKey(attribute.NamespacePrefix) ||
                            namespaceMap[attribute.NamespacePrefix] != attribute.Namespace)
                        {
                            xtw.WriteAttributeString("xmlns", attribute.NamespacePrefix, null, attribute.Namespace);
                            namespaceMap[attribute.NamespacePrefix] = attribute.Namespace;
                        }
                        xtw.WriteAttributeString(attribute.NamespacePrefix, attribute.Name, attribute.Namespace, attribute.Value);
                    }
                    else
                    {
                        xtw.WriteAttributeString(attribute.Namespace, attribute.Name, attribute.Value);
                    }
                }
                else
                {
                    xtw.WriteAttributeString(attribute.Name, attribute.Value);
                }
            }
        }

        if (extensionElement.ElementText != null)
        {
            xtw.WriteCData(extensionElement.ElementText);
        }
        else
        {
            foreach (var childElement in extensionElement.ChildElements)
            {
                WriteExtensionElement(childElement, namespaceMap, xtw);
            }
        }

        foreach (var prefix in localNamespaceMap.Keys)
            namespaceMap.Remove(prefix);

        xtw.WriteEndElement();
    }

    public static List<string> ParseDelimitedList(string? s)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(s)) return result;

        var strb = new StringBuilder();
        bool insideExpression = false;

        foreach (char c in s!)
        {
            if (c == '{' || c == '$')
                insideExpression = true;
            else if (c == '}')
                insideExpression = false;
            else if (c == ',' && !insideExpression)
            {
                result.Add(strb.ToString().Trim());
                strb.Clear();
                continue;
            }

            if (c != ',' || insideExpression)
                strb.Append(c);
        }

        if (strb.Length > 0)
            result.Add(strb.ToString().Trim());

        return result;
    }

    public static string ConvertToDelimitedString(List<string> stringList)
    {
        if (stringList == null) return string.Empty;
        return string.Join(",", stringList);
    }

    public static void WriteIncomingAndOutgoingFlowElement(BpmnModelNs.FlowNode flowNode, XmlWriter xtw)
    {
        if (flowNode.IncomingFlows.Count > 0)
        {
            foreach (var incomingSequence in flowNode.IncomingFlows)
            {
                xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_GATEWAY_INCOMING, BpmnXMLConstants.BPMN2_NAMESPACE);
                xtw.WriteString(incomingSequence.Id);
                xtw.WriteEndElement();
            }
        }

        if (flowNode.OutgoingFlows.Count > 0)
        {
            foreach (var outgoingSequence in flowNode.OutgoingFlows)
            {
                xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_GATEWAY_OUTGOING, BpmnXMLConstants.BPMN2_NAMESPACE);
                xtw.WriteString(outgoingSequence.Id);
                xtw.WriteEndElement();
            }
        }
    }

    public static bool IsBlacklisted(BpmnModelNs.ExtensionAttribute attribute, params List<BpmnModelNs.ExtensionAttribute>[]? blackLists)
    {
        if (blackLists == null) return false;
        foreach (var blackList in blackLists)
        {
            if (blackList == null) continue;
            foreach (var blackAttribute in blackList)
            {
                if (blackAttribute.Name == attribute.Name)
                {
                    if (blackAttribute.Namespace != null && attribute.Namespace != null && blackAttribute.Namespace == attribute.Namespace)
                        return true;
                    if (blackAttribute.Namespace == null && attribute.Namespace == null)
                        return true;
                }
            }
        }
        return false;
    }

    public static void WriteCustomAttributes(
        IEnumerable<List<BpmnModelNs.ExtensionAttribute>> attributes,
        XmlWriter xtw,
        Dictionary<string, string> namespaceMap,
        params List<BpmnModelNs.ExtensionAttribute>[]? blackLists)
    {
        foreach (var attributeList in attributes)
        {
            if (attributeList == null || attributeList.Count == 0) continue;
            foreach (var attribute in attributeList)
            {
                if (IsBlacklisted(attribute, blackLists)) continue;
                if (string.IsNullOrEmpty(attribute.Name) || attribute.Value == null) continue;

                if (string.IsNullOrEmpty(attribute.NamespacePrefix))
                {
                    if (string.IsNullOrEmpty(attribute.Namespace))
                        xtw.WriteAttributeString(attribute.Name, attribute.Value);
                    else
                        xtw.WriteAttributeString(attribute.Namespace, attribute.Name, attribute.Value);
                }
                else
                {
                    if (!namespaceMap.ContainsKey(attribute.NamespacePrefix!))
                    {
                        namespaceMap[attribute.NamespacePrefix!] = attribute.Namespace!;
                        xtw.WriteAttributeString("xmlns", attribute.NamespacePrefix, null, attribute.Namespace);
                    }
                    xtw.WriteAttributeString(attribute.NamespacePrefix!, attribute.Name, attribute.Namespace!, attribute.Value);
                }
            }
        }
    }

    private static void ParseExtensionChildElements(
        XmlNode extensionsNode,
        BpmnModelNs.BaseElement parentElement,
        Dictionary<string, BaseChildElementParser> localParserMap,
        BpmnModelNs.BpmnModel model)
    {
        foreach (XmlNode child in extensionsNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;

            if (localParserMap.TryGetValue(child.LocalName, out var parser))
            {
                if (parser.Accepts(parentElement))
                {
                    parser.ParseChildElement(child, parentElement, model);
                    continue;
                }
            }

            var extensionElement = ParseExtensionElement(child);
            parentElement.AddExtensionElement(extensionElement);
        }
    }
}

public class CommaSplitter
{
    public static List<string> Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<string>();
        return value!.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }
}

public delegate Stream InputStreamProvider();

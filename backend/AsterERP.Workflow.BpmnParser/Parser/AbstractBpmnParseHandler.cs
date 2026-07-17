using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public abstract class AbstractBpmnParseHandler : IBpmnParseHandler
{
    public abstract string[] HandledTypes { get; }

    public void Parse(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var element = ParseElement(xmlNode, model, activeProcess);
        if (element != null)
        {
            PostProcessElement(xmlNode, element, model, activeProcess);
            activeProcess.FlowElements.Add(element);
        }
    }

    protected abstract FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess);

    protected virtual void PostProcessElement(XmlNode xmlNode, FlowElement element, BpmnModel.BpmnModel model, Process activeProcess)
    {
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

    protected static bool ParseBoolean(XmlNode node, string localName, bool defaultValue = false)
    {
        var value = GetAttributeValue(node, localName);
        if (value == null) return defaultValue;
        return value.ToLowerInvariant() == "true";
    }

    protected static int? ParseInt(XmlNode node, string localName)
    {
        var value = GetAttributeValue(node, localName);
        return int.TryParse(value, out var result) ? result : null;
    }

    protected static int? ParseInt(XmlNode node, string prefix, string localName)
    {
        var value = GetAttributeValue(node, prefix, localName);
        return int.TryParse(value, out var result) ? result : null;
    }

    protected static bool ParseBoolean(XmlNode node, string prefix, string localName, bool defaultValue = false)
    {
        var value = GetAttributeValue(node, prefix, localName);
        if (value == null) return defaultValue;
        return value.ToLowerInvariant() == "true";
    }

    protected static List<string> ParseCommaSeparatedList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<string>();
        return value.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }
}

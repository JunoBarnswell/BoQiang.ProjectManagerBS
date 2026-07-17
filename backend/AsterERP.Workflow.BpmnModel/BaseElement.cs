using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace AsterERP.Workflow.BpmnModel;

public interface IHasExtensionAttributes
{
    Dictionary<string, List<ExtensionAttribute>> Attributes { get; }
    string? GetAttributeValue(string? namespaceName, string name);
    void AddAttribute(ExtensionAttribute attribute);
}

public abstract class BaseElement : IHasExtensionAttributes
{
    public string? Id { get; set; }
    public int XmlRowNumber { get; set; }
    public int XmlColumnNumber { get; set; }

    protected Dictionary<string, List<ExtensionElement>> _extensionElements = new();

    public Dictionary<string, List<ExtensionElement>> ExtensionElements
    {
        get => _extensionElements;
        set => _extensionElements = value;
    }

    public void AddExtensionElement(ExtensionElement extensionElement)
    {
        if (extensionElement != null && !string.IsNullOrEmpty(extensionElement.Name))
        {
            if (!_extensionElements.ContainsKey(extensionElement.Name))
                _extensionElements[extensionElement.Name] = new List<ExtensionElement>();
            _extensionElements[extensionElement.Name].Add(extensionElement);
        }
    }

    public Dictionary<string, List<ExtensionAttribute>> Attributes { get; set; } = new();

    public virtual string? GetAttributeValue(string? namespaceName, string name)
    {
        if (Attributes.TryGetValue(name, out var attrs))
        {
            var match = attrs.Find(a =>
                (namespaceName == null && a.Namespace == null) ||
                (namespaceName != null && namespaceName == a.Namespace));
            return match?.Value;
        }
        return null;
    }

    public virtual void AddAttribute(ExtensionAttribute attribute)
    {
        if (attribute != null && !string.IsNullOrEmpty(attribute.Name))
        {
            if (!Attributes.ContainsKey(attribute.Name))
                Attributes[attribute.Name] = new List<ExtensionAttribute>();
            Attributes[attribute.Name].Add(attribute);
        }
    }

    public void SetValues(BaseElement otherElement)
    {
        Id = otherElement.Id;

        if (otherElement._extensionElements.Count > 0)
        {
            foreach (var kvp in otherElement._extensionElements)
            {
                if (kvp.Value is { Count: > 0 })
                    _extensionElements[kvp.Key] = new List<ExtensionElement>(kvp.Value);
            }
        }

        if (otherElement.Attributes.Count > 0)
        {
            foreach (var kvp in otherElement.Attributes)
            {
                if (kvp.Value is { Count: > 0 })
                    Attributes[kvp.Key] = new List<ExtensionAttribute>(kvp.Value);
            }
        }
    }

    public abstract BaseElement Clone();
}

public class ExtensionAttribute
{
    public string? Namespace { get; set; }
    public string? Name { get; set; }
    public string? Value { get; set; }
    public string? NamespacePrefix { get; set; }

    public ExtensionAttribute Clone()
    {
        return new ExtensionAttribute
        {
            Namespace = Namespace,
            Name = Name,
            Value = Value,
            NamespacePrefix = NamespacePrefix
        };
    }
}

public class ExtensionElement : BaseElement
{
    public string? Name { get; set; }
    public string? Namespace { get; set; }
    public string? NamespacePrefix { get; set; }
    public string? ElementText { get; set; }

    public new List<ExtensionAttribute> Attributes { get; set; } = new();
    public List<ExtensionElement> ChildElements { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new ExtensionElement
        {
            Id = Id,
            Name = Name,
            Namespace = Namespace,
            NamespacePrefix = NamespacePrefix,
            ElementText = ElementText,
            XmlRowNumber = XmlRowNumber,
            XmlColumnNumber = XmlColumnNumber
        };
        clone.Attributes.AddRange(Attributes.Select(a => a.Clone()));
        clone.ChildElements.AddRange(ChildElements.Select(c => (ExtensionElement)c.Clone()));
        return clone;
    }
}

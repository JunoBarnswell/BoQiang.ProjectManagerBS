using System;
using System.Collections.Generic;
using System.Xml;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class DefinitionsParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var targetNamespace = GetAttributeValue(xmlNode, "targetNamespace");
        if (!string.IsNullOrEmpty(targetNamespace))
            model.TargetNamespace = targetNamespace;
        else
            model.TargetNamespace = BpmnXMLConstants.PROCESS_NAMESPACE;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class ProcessParser
{
    public BpmnModelNs.Process? Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var process = new BpmnModelNs.Process
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            IsExecutable = GetAttributeValue(xmlNode, "isExecutable")?.ToLowerInvariant() != "false"
        };

        var candidateStarterUsers = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "candidateStarterUsers");
        if (!string.IsNullOrEmpty(candidateStarterUsers))
            process.CandidateStarterUsers = candidateStarterUsers;

        var candidateStarterGroups = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "candidateStarterGroups");
        if (!string.IsNullOrEmpty(candidateStarterGroups))
            process.CandidateStarterGroups = candidateStarterGroups;

        return process;
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
            if (attr.LocalName == localName && attr.Prefix == prefix)
                return attr.Value;
        }
        return null;
    }
}

public class SubProcessParser
{
    public void Parse(XmlNode xmlNode, List<BpmnModelNs.SubProcess> activeSubProcessList, BpmnModelNs.Process activeProcess)
    {
        var subProcess = new BpmnModelNs.SubProcess
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            TriggeredByEvent = GetAttributeValue(xmlNode, "triggeredByEvent") == "true"
        };

        var async = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "async");
        subProcess.Asynchronous = async?.ToLowerInvariant() == "true";

        activeSubProcessList.Add(subProcess);
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
            if (attr.LocalName == localName && attr.Prefix == prefix)
                return attr.Value;
        }
        return null;
    }
}

public class MessageParser
{
    public BpmnModelNs.Message Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var message = new BpmnModelNs.Message
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            ItemRef = ParseItemRef(GetAttributeValue(xmlNode, "itemRef"), xmlNode, model)
        };

        if (!string.IsNullOrEmpty(message.Id))
            model.MessageMap[message.Id] = message;
        return message;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }

    private static string? ParseItemRef(string? itemRef, XmlNode node, BpmnModelNs.BpmnModel model)
    {
        if (string.IsNullOrEmpty(itemRef))
            return null;

        var prefixIndex = itemRef.IndexOf(':');
        if (prefixIndex >= 0)
        {
            var prefix = itemRef[..prefixIndex];
            var localName = itemRef[(prefixIndex + 1)..];
            var namespaceUri = node.GetNamespaceOfPrefix(prefix);
            return string.IsNullOrEmpty(namespaceUri) ? itemRef : $"{namespaceUri}:{localName}";
        }

        var targetNamespace = model.TargetNamespace;
        if (string.IsNullOrEmpty(targetNamespace))
            targetNamespace = BpmnXMLConstants.PROCESS_NAMESPACE;

        return $"{targetNamespace}:{itemRef}";
    }
}

public class ErrorParser
{
    public BpmnModelNs.Error? Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var error = new BpmnModelNs.Error
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            ErrorCode = GetAttributeValue(xmlNode, "errorCode")
        };

        if (!string.IsNullOrEmpty(error.Id))
            model.ErrorMap[error.Id] = error;

        return error;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class EscalationParser
{
    public BpmnModelNs.Escalation? Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var escalation = new BpmnModelNs.Escalation
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            EscalationCode = GetAttributeValue(xmlNode, "escalationCode")
        };

        if (!string.IsNullOrEmpty(escalation.Id))
            model.EscalationMap[escalation.Id] = escalation;

        return escalation;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class MessageFlowParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var messageFlow = new BpmnModelNs.MessageFlow
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            SourceRef = GetAttributeValue(xmlNode, "sourceRef"),
            TargetRef = GetAttributeValue(xmlNode, "targetRef"),
            MessageRef = GetAttributeValue(xmlNode, "messageRef")
        };

        model.MessageFlows.Add(messageFlow);
        if (!string.IsNullOrEmpty(messageFlow.Id))
            model.MessageFlowMap[messageFlow.Id] = messageFlow;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class SignalParser
{
    public BpmnModelNs.Signal Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var signal = new BpmnModelNs.Signal
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name")
        };

        var scope = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "scope");
        if (!string.IsNullOrEmpty(scope))
            signal.Scope = scope;

        model.Signals.Add(signal);
        return signal;
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
            if (attr.LocalName == localName && attr.Prefix == prefix)
                return attr.Value;
        }
        return null;
    }
}

public class ParticipantParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var pool = new BpmnModelNs.Pool
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            ProcessRef = GetAttributeValue(xmlNode, "processRef")
        };

        model.Pools.Add(pool);
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class LaneParser
{
    public BpmnModelNs.Lane Parse(XmlNode xmlNode, BpmnModelNs.Process activeProcess, BpmnModelNs.BpmnModel model)
    {
        var lane = new BpmnModelNs.Lane
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "flowNodeRef")
                lane.FlowReferences.Add(child.InnerText);
        }

        return lane;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class InterfaceParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var iface = new BpmnModelNs.Interface
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "operation")
            {
                var inMessageRef = GetAttributeValue(child, "inMessageRef");
                var outMessageRef = GetAttributeValue(child, "outMessageRef");
                var operation = new BpmnModelNs.Operation
                {
                    Id = GetAttributeValue(child, "id"),
                    Name = GetAttributeValue(child, "name"),
                    ImplementationRef = GetAttributeValue(child, "implementationRef"),
                    InMessage = !string.IsNullOrWhiteSpace(inMessageRef) ? new BpmnModelNs.Message { Id = inMessageRef } : null,
                    OutMessage = !string.IsNullOrWhiteSpace(outMessageRef) ? new BpmnModelNs.Message { Id = outMessageRef } : null
                };
                iface.Operations.Add(operation);
            }
        }

        model.Interfaces.Add(iface);
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class ItemDefinitionParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var itemDef = new BpmnModelNs.ItemDefinition
        {
            Id = GetAttributeValue(xmlNode, "id"),
            StructureRef = GetAttributeValue(xmlNode, "structureRef"),
            ItemKind = GetAttributeValue(xmlNode, "itemKind")
        };

        var isCollection = GetAttributeValue(xmlNode, "isCollection");
        if (!string.IsNullOrEmpty(isCollection))
            itemDef.IsCollection = isCollection.ToLowerInvariant() == "true";

        if (!string.IsNullOrEmpty(itemDef.Id))
            model.ItemDefinitionMap[itemDef.Id] = itemDef;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class DataStoreParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var dataStore = new BpmnModelNs.DataStore
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            ItemSubjectRef = GetAttributeValue(xmlNode, "itemSubjectRef")
        };

        var capacity = GetAttributeValue(xmlNode, "capacity");
        if (!string.IsNullOrEmpty(capacity))
        {
            dataStore.Capacity = capacity;
            dataStore.IsUnlimited = false;
        }

        if (!string.IsNullOrEmpty(dataStore.Id))
            model.DataStoreMap[dataStore.Id] = dataStore;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class ImportParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var import = new BpmnModelNs.Import
        {
            ImportType = GetAttributeValue(xmlNode, "importType"),
            Location = GetAttributeValue(xmlNode, "location"),
            Namespace = GetAttributeValue(xmlNode, "namespace")
        };
        model.Imports.Add(import);
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class ResourceParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var resource = new BpmnModelNs.Resource
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name")
        };
        model.Resources.Add(resource);
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class PotentialStarterParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.Process activeProcess)
    {
    }
}

public class ExtensionElementsParser
{
    public void Parse(XmlNode xmlNode, List<BpmnModelNs.SubProcess> activeSubProcessList, BpmnModelNs.Process activeProcess, BpmnModelNs.BpmnModel model)
    {
        BpmnModelNs.BaseElement parentElement = activeProcess;
        if (activeSubProcessList.Count > 0)
            parentElement = activeSubProcessList[^1];

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            var extensionElement = BpmnXMLUtil.ParseExtensionElement(child);
            parentElement.AddExtensionElement(extensionElement);
        }
    }
}

public class BpmnShapeParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var bpmnElement = GetAttributeValue(xmlNode, "bpmnElement");
        if (string.IsNullOrEmpty(bpmnElement)) return;

        var graphicInfo = new BpmnModelNs.GraphicInfo
        {
            Element = bpmnElement
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "Bounds")
            {
                var x = GetAttributeValue(child, "x");
                var y = GetAttributeValue(child, "y");
                var width = GetAttributeValue(child, "width");
                var height = GetAttributeValue(child, "height");

                if (double.TryParse(x, out var xVal)) graphicInfo.X = xVal;
                if (double.TryParse(y, out var yVal)) graphicInfo.Y = yVal;
                if (double.TryParse(width, out var wVal)) graphicInfo.Width = wVal;
                if (double.TryParse(height, out var hVal)) graphicInfo.Height = hVal;
            }
            else if (child.LocalName == "BPMNLabel")
            {
                foreach (XmlNode labelChild in child.ChildNodes)
                {
                    if (labelChild.NodeType != XmlNodeType.Element || labelChild.LocalName != "Bounds") continue;

                    var lx = GetAttributeValue(labelChild, "x");
                    var ly = GetAttributeValue(labelChild, "y");
                    var lwidth = GetAttributeValue(labelChild, "width");
                    var lheight = GetAttributeValue(labelChild, "height");

                    var labelInfo = new BpmnModelNs.GraphicInfo { Element = bpmnElement };
                    if (double.TryParse(lx, out var lxVal)) labelInfo.X = lxVal;
                    if (double.TryParse(ly, out var lyVal)) labelInfo.Y = lyVal;
                    if (double.TryParse(lwidth, out var lwVal)) labelInfo.Width = lwVal;
                    if (double.TryParse(lheight, out var lhVal)) labelInfo.Height = lhVal;
                    model.LabelLocationMap[bpmnElement!] = labelInfo;
                }
            }
        }

        model.LocationMap[bpmnElement!] = graphicInfo;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

public class BpmnEdgeParser
{
    public void Parse(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        var bpmnElement = GetAttributeValue(xmlNode, "bpmnElement");
        if (string.IsNullOrEmpty(bpmnElement)) return;

        var waypoints = new List<BpmnModelNs.GraphicInfo>();
        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "waypoint")
            {
                var x = GetAttributeValue(child, "x");
                var y = GetAttributeValue(child, "y");
                var point = new BpmnModelNs.GraphicInfo();
                if (double.TryParse(x, out var xVal)) point.X = xVal;
                if (double.TryParse(y, out var yVal)) point.Y = yVal;
                waypoints.Add(point);
            }
        }

        model.FlowLocationMap[bpmnElement!] = waypoints;
    }

    private static string? GetAttributeValue(XmlNode node, string localName)
    {
        return node.Attributes?[localName]?.Value;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class BpmnXmlParser
{
    private const string BpmnNamespace = "http://www.omg.org/spec/BPMN/20100524/MODEL";

    private readonly List<(BpmnModelNs.Association Association, BpmnModelNs.Process Process)> _pendingAssociations = new();
    private readonly List<(BpmnModelNs.Lane Lane, BpmnModelNs.Process Process)> _pendingLanes = new();

    public BpmnModelNs.BpmnModel Parse(string xmlContent)
    {
        _pendingAssociations.Clear();
        _pendingLanes.Clear();

        var definitions = LoadDefinitions(xmlContent);
        var model = CreateModel(definitions);

        ParseDefinitionsChildren(definitions, model);
        ResolveAndValidateModel(model);

        return model;
    }

    private static XmlElement LoadDefinitions(string xmlContent)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlContent);
        return xmlDoc.DocumentElement!;
    }

    private BpmnModelNs.BpmnModel CreateModel(XmlElement definitions)
    {
        var model = new BpmnModelNs.BpmnModel();
        model.TargetNamespace = GetAttributeValue(definitions, "targetNamespace") ?? BpmnConstants.TargetNamespace;
        return model;
    }

    private void ParseDefinitionsChildren(XmlElement definitions, BpmnModelNs.BpmnModel model)
    {
        BpmnModelNs.Process? activeProcess = null;

        foreach (XmlNode node in definitions.ChildNodes)
        {
            if (node.NodeType != XmlNodeType.Element) continue;

            ParseDefinitionsChild(node, model, ref activeProcess);
        }
    }

    private void ParseDefinitionsChild(XmlNode node, BpmnModelNs.BpmnModel model, ref BpmnModelNs.Process? activeProcess)
    {
        switch (node.LocalName)
        {
            case "process":
                var process = ParseProcess(node);
                model.AddProcess(process);
                activeProcess = process;
                break;
            case "collaboration":
                ParseCollaboration(node, model);
                break;
            case "message":
                var message = ParseMessage(node);
                if (!string.IsNullOrEmpty(message.Id))
                    model.MessageMap[message.Id] = message;
                break;
            case "signal":
                model.Signals.Add(ParseSignal(node));
                break;
            case "escalation":
                var escalation = new BpmnModelNs.Escalation
                {
                    Id = GetAttributeValue(node, "id"),
                    Name = GetAttributeValue(node, "name"),
                    EscalationCode = GetAttributeValue(node, "escalationCode")
                };
                if (!string.IsNullOrEmpty(escalation.Id))
                    model.EscalationMap[escalation.Id] = escalation;
                break;
            case "dataStore":
                var dataStore = ParseDataStore(node);
                if (!string.IsNullOrEmpty(dataStore.Id))
                    model.DataStoreMap[dataStore.Id] = dataStore;
                break;
            case "itemDefinition":
                var itemDefinition = ParseItemDefinition(node);
                if (!string.IsNullOrEmpty(itemDefinition.Id))
                    model.ItemDefinitionMap[itemDefinition.Id] = itemDefinition;
                break;
            case "import":
                model.Imports.Add(ParseImport(node));
                break;
            case "interface":
                model.Interfaces.Add(ParseInterface(node));
                break;
            case "resource":
                model.Resources.Add(ParseResource(node));
                break;
            case "participant":
                model.Pools.Add(new BpmnModelNs.Pool
                {
                    Id = GetAttributeValue(node, "id"),
                    Name = GetAttributeValue(node, "name"),
                    ProcessRef = GetAttributeValue(node, "processRef")
                });
                break;
            case "messageFlow":
                var messageFlow = new BpmnModelNs.MessageFlow
                {
                    Id = GetAttributeValue(node, "id"),
                    Name = GetAttributeValue(node, "name"),
                    SourceRef = GetAttributeValue(node, "sourceRef"),
                    TargetRef = GetAttributeValue(node, "targetRef"),
                    MessageRef = GetAttributeValue(node, "messageRef")
                };
                model.MessageFlows.Add(messageFlow);
                if (!string.IsNullOrEmpty(messageFlow.Id))
                    model.MessageFlowMap[messageFlow.Id] = messageFlow;
                break;
            case "lane":
                ParseTopLevelLane(node, activeProcess);
                break;
            case "potentialStarter":
                ValidatePotentialStarterScope(node, activeProcess);
                break;
            default:
                ValidateUnsupportedTopLevelElement(node);
                break;
        }
    }

    private void ParseTopLevelLane(XmlNode node, BpmnModelNs.Process? activeProcess)
    {
        if (activeProcess != null)
        {
            var lane = ParseLane(node);
            _pendingLanes.Add((lane, activeProcess));
        }
        else
        {
            var laneId = GetAttributeValue(node, "id") ?? "<unknown>";
            throw new WorkflowEngineException(
                $"Element 'lane' must be declared inside a process (id='{laneId}').");
        }
    }

    private void ValidatePotentialStarterScope(XmlNode node, BpmnModelNs.Process? activeProcess)
    {
        if (activeProcess == null)
        {
            var resourceRef = GetAttributeValue(node, "resourceRef") ?? "<unknown>";
            throw new WorkflowEngineException(
                $"Element 'potentialStarter' must be declared inside a process (resourceRef='{resourceRef}').");
        }
    }

    private void ValidateUnsupportedTopLevelElement(XmlNode node)
    {
        var converter = BpmnConverterRegistry.GetConverter(node.LocalName);
        if (converter != null)
        {
            throw new WorkflowEngineException(
                $"Flow element '{node.LocalName}' must be declared inside a process.");
        }
        if (node.NamespaceURI == BpmnNamespace || string.IsNullOrEmpty(node.NamespaceURI))
        {
            var elementId = GetAttributeValue(node, "id") ?? "<unknown>";
            throw new WorkflowEngineException(
                $"Unsupported BPMN element '{node.LocalName}' (id='{elementId}').");
        }
    }

    private void ResolveAndValidateModel(BpmnModelNs.BpmnModel model)
    {
        foreach (var proc in model.Processes)
        {
            ResolveFlowElementReferences(proc);
            ValidateFlowElements(proc.FlowElements, proc);
        }

        ValidateAssociations();
        ValidateMessageFlows(model);
        ValidatePoolProcessReferences(model);
        ValidateLaneFlowReferences();
    }

    private void ResolveFlowElementReferences(BpmnModelNs.Process process)
    {
        ResolveFlowElementReferencesForContainer(process.FlowElements);

        foreach (var flowElement in process.FlowElements)
        {
            if (flowElement is BpmnModelNs.SubProcess subProcess)
            {
                ResolveFlowElementReferencesForSubProcess(subProcess);
            }
        }
    }

    private void ResolveFlowElementReferencesForContainer(List<BpmnModelNs.FlowElement> flowElements)
    {
        foreach (var flowElement in flowElements)
        {
            if (flowElement is BpmnModelNs.SequenceFlow sequenceFlow)
            {
                if (!string.IsNullOrEmpty(sequenceFlow.TargetRef))
                {
                    sequenceFlow.TargetFlowElement = flowElements.FirstOrDefault(e => e.Id == sequenceFlow.TargetRef) as BpmnModelNs.FlowNode;
                }
            }
        }

        foreach (var flowElement in flowElements)
        {
            if (flowElement is BpmnModelNs.FlowNode flowNode)
            {
                var outgoingFlows = flowElements
                    .OfType<BpmnModelNs.SequenceFlow>()
                    .Where(sf => sf.SourceRef == flowNode.Id)
                    .ToList();
                flowNode.OutgoingFlows = outgoingFlows;

                var incomingFlows = flowElements
                    .OfType<BpmnModelNs.SequenceFlow>()
                    .Where(sf => sf.TargetRef == flowNode.Id)
                    .ToList();
                flowNode.IncomingFlows = incomingFlows;
            }
        }

        foreach (var boundaryEvent in flowElements.OfType<BpmnModelNs.BoundaryEvent>())
        {
            if (string.IsNullOrEmpty(boundaryEvent.AttachedToRefId))
            {
                continue;
            }

            var attachedActivity = flowElements
                .OfType<BpmnModelNs.Activity>()
                .FirstOrDefault(activity => activity.Id == boundaryEvent.AttachedToRefId);

            if (attachedActivity == null)
            {
                continue;
            }

            boundaryEvent.AttachedToRef = attachedActivity;
            if (!attachedActivity.BoundaryEvents.Contains(boundaryEvent))
            {
                attachedActivity.BoundaryEvents.Add(boundaryEvent);
            }
        }
    }

    private void ResolveFlowElementReferencesForSubProcess(BpmnModelNs.SubProcess subProcess)
    {
        ResolveFlowElementReferencesForContainer(subProcess.FlowElements);

        foreach (var flowElement in subProcess.FlowElements)
        {
            if (flowElement is BpmnModelNs.SubProcess innerSubProcess)
            {
                ResolveFlowElementReferencesForSubProcess(innerSubProcess);
            }
        }
    }

    private void ValidateFlowElements(List<BpmnModelNs.FlowElement> flowElements, BpmnModelNs.Process process)
    {
        foreach (var flowElement in flowElements)
        {
            if (flowElement is BpmnModelNs.SequenceFlow sequenceFlow)
            {
                if (string.IsNullOrEmpty(sequenceFlow.SourceRef))
                {
                    throw new WorkflowEngineException(
                        $"SequenceFlow '{sequenceFlow.Id ?? "<unknown>"}' requires non-empty sourceRef.");
                }

                if (!string.IsNullOrEmpty(sequenceFlow.TargetRef) && sequenceFlow.TargetFlowElement == null)
                {
                    throw new WorkflowEngineException(
                        $"SequenceFlow '{sequenceFlow.Id ?? "<unknown>"}' references missing targetRef '{sequenceFlow.TargetRef}'.");
                }
            }
            else if (flowElement is BpmnModelNs.BoundaryEvent boundaryEvent)
            {
                if (!string.IsNullOrEmpty(boundaryEvent.AttachedToRefId))
                {
                    var attachedElement = FindFlowElementRecursive(process.FlowElements, boundaryEvent.AttachedToRefId);
                    if (attachedElement == null)
                    {
                        throw new WorkflowEngineException(
                            $"BoundaryEvent '{boundaryEvent.Id ?? "<unknown>"}' references missing attachedToRef '{boundaryEvent.AttachedToRefId}'.");
                    }
                }
            }
            else if (flowElement is BpmnModelNs.EventGateway eventGateway)
            {
                ValidateEventGatewayOutgoingTargets(eventGateway);
            }
            else if (flowElement is BpmnModelNs.ExclusiveGateway exclusiveGateway)
            {
                ValidateGatewayDefaultFlow(exclusiveGateway, process);
            }
            else if (flowElement is BpmnModelNs.InclusiveGateway inclusiveGateway)
            {
                ValidateGatewayDefaultFlow(inclusiveGateway, process);
            }
            else if (flowElement is BpmnModelNs.ComplexGateway complexGateway)
            {
                ValidateGatewayDefaultFlow(complexGateway, process);
            }
            else if (flowElement is BpmnModelNs.SubProcess subProcess)
            {
                ValidateFlowElements(subProcess.FlowElements, process);
            }
        }
    }

    private static void ValidateEventGatewayOutgoingTargets(BpmnModelNs.EventGateway eventGateway)
    {
        foreach (var outgoingFlow in eventGateway.OutgoingFlows)
        {
            if (outgoingFlow.TargetFlowElement is BpmnModelNs.IntermediateCatchEvent or BpmnModelNs.ReceiveTask)
            {
                continue;
            }

            throw new WorkflowEngineException(
                $"EventGateway '{eventGateway.Id ?? "<unknown>"}' has invalid outgoing target '{outgoingFlow.TargetRef ?? "<null>"}'. Target must be IntermediateCatchEvent or ReceiveTask.");
        }
    }

    private static void ValidateGatewayDefaultFlow(BpmnModelNs.Gateway gateway, BpmnModelNs.Process process)
    {
        var defaultFlowId = gateway switch
        {
            BpmnModelNs.ExclusiveGateway e => e.DefaultFlow,
            BpmnModelNs.InclusiveGateway i => i.DefaultFlow,
            BpmnModelNs.ComplexGateway c => c.DefaultFlow,
            _ => null
        };

        if (string.IsNullOrEmpty(defaultFlowId))
            return;

        var defaultFlowElement = process.FlowElements.FirstOrDefault(e => e.Id == defaultFlowId);
        if (defaultFlowElement is not BpmnModelNs.SequenceFlow)
        {
            throw new WorkflowEngineException(
                $"Gateway '{gateway.Id ?? "<unknown>"}' references missing default flow '{defaultFlowId}'.");
        }
    }

    private void ValidateAssociations()
    {
        foreach (var (association, process) in _pendingAssociations)
        {
            if (string.IsNullOrEmpty(association.SourceRef))
            {
                throw new WorkflowEngineException(
                    $"Association '{association.Id ?? "<unknown>"}' requires non-empty sourceRef.");
            }

            if (!string.IsNullOrEmpty(association.TargetRef))
            {
                var targetExists = ProcessElementExists(process, association.TargetRef);
                if (!targetExists)
                {
                    throw new WorkflowEngineException(
                        $"Association '{association.Id ?? "<unknown>"}' references missing targetRef '{association.TargetRef}'.");
                }
            }
        }
    }

    private static void ValidateMessageFlows(BpmnModelNs.BpmnModel model)
    {
        foreach (var messageFlow in model.MessageFlows)
        {
            if (!string.IsNullOrEmpty(messageFlow.SourceRef) && !model.Pools.Any(p => p.Id == messageFlow.SourceRef))
            {
                throw new WorkflowEngineException(
                    $"MessageFlow '{messageFlow.Id ?? "<unknown>"}' references missing sourceRef '{messageFlow.SourceRef}'.");
            }

            if (!string.IsNullOrEmpty(messageFlow.TargetRef) && !model.Pools.Any(p => p.Id == messageFlow.TargetRef))
            {
                throw new WorkflowEngineException(
                    $"MessageFlow '{messageFlow.Id ?? "<unknown>"}' references missing targetRef '{messageFlow.TargetRef}'.");
            }

            if (!string.IsNullOrEmpty(messageFlow.MessageRef) && !model.MessageMap.ContainsKey(messageFlow.MessageRef))
            {
                throw new WorkflowEngineException(
                    $"MessageFlow '{messageFlow.Id ?? "<unknown>"}' references missing messageRef '{messageFlow.MessageRef}'.");
            }
        }
    }

    private static void ValidatePoolProcessReferences(BpmnModelNs.BpmnModel model)
    {
        foreach (var pool in model.Pools)
        {
            if (string.IsNullOrEmpty(pool.ProcessRef))
                continue;

            if (!model.Processes.Any(p => p.Id == pool.ProcessRef))
            {
                throw new WorkflowEngineException(
                    $"Participant '{pool.Id ?? "<unknown>"}' references missing processRef '{pool.ProcessRef}'.");
            }
        }
    }

    private void ValidateLaneFlowReferences()
    {
        foreach (var (lane, process) in _pendingLanes)
        {
            foreach (var flowRef in lane.FlowReferences)
            {
                if (string.IsNullOrEmpty(flowRef))
                    continue;

                if (!FlowElementExists(process.FlowElements, flowRef))
                {
                    throw new WorkflowEngineException(
                        $"Lane '{lane.Id ?? "<unknown>"}' references missing flowNodeRef '{flowRef}'.");
                }
            }
        }
    }

    private static bool FlowElementExists(List<BpmnModelNs.FlowElement> flowElements, string flowElementId)
    {
        foreach (var flowElement in flowElements)
        {
            if (string.Equals(flowElement.Id, flowElementId, StringComparison.Ordinal))
                return true;

            if (flowElement is BpmnModelNs.SubProcess subProcess &&
                FlowElementExists(subProcess.FlowElements, flowElementId))
                return true;
        }

        return false;
    }

    private static bool ProcessElementExists(BpmnModelNs.Process process, string elementId)
    {
        if (FlowElementExists(process.FlowElements, elementId))
            return true;

        if (process.Artifacts.Any(artifact => string.Equals(artifact.Id, elementId, StringComparison.Ordinal)))
            return true;

        if (process.DataStoreReferences.Any(dataStoreReference => string.Equals(dataStoreReference.Id, elementId, StringComparison.Ordinal)))
            return true;

        if (process.ValuedDataObjects.Any(dataObject => string.Equals(dataObject.Id, elementId, StringComparison.Ordinal)))
            return true;

        return process.DataObjects.Any(dataObject => string.Equals(dataObject.Id, elementId, StringComparison.Ordinal));
    }

    private static BpmnModelNs.FlowElement? FindFlowElementRecursive(List<BpmnModelNs.FlowElement> flowElements, string flowElementId)
    {
        foreach (var flowElement in flowElements)
        {
            if (string.Equals(flowElement.Id, flowElementId, StringComparison.Ordinal))
                return flowElement;

            if (flowElement is BpmnModelNs.SubProcess subProcess)
            {
                var found = FindFlowElementRecursive(subProcess.FlowElements, flowElementId);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private BpmnModelNs.Process ParseProcess(XmlNode node)
    {
        var process = new BpmnModelNs.Process
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            IsExecutable = bool.Parse(GetAttributeValue(node, "isExecutable") ?? "true"),
            CandidateStarterUsers = GetAttributeValue(node, "activiti", "candidateStarterUsers"),
            CandidateStarterGroups = GetAttributeValue(node, "activiti", "candidateStarterGroups")
        };

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "documentation")
            {
                process.Documentation = child.InnerText;
                continue;
            }
            if (child.LocalName == "error")
            {
                process.Errors.Add(ParseError(child));
                continue;
            }
            if (child.LocalName == "signal")
            {
                process.Signals.Add(ParseSignal(child));
                continue;
            }
            var flowElement = ParseFlowElement(child, process);
            if (flowElement is BpmnModelNs.FlowElement fe)
            {
                fe.ParentContainer = process;
                process.FlowElements.Add(fe);
            }
            else if (flowElement is BpmnModelNs.Association association)
                _pendingAssociations.Add((association, process));
            else if (flowElement is BpmnModelNs.Artifact artifact)
                process.Artifacts.Add(artifact);
            else if (flowElement is BpmnModelNs.DataStoreReference dataStoreReference)
                process.DataStoreReferences.Add(dataStoreReference);
            else if (flowElement is BpmnModelNs.ValuedDataObject valuedDataObject)
                process.ValuedDataObjects.Add(valuedDataObject);
        }

        return process;
    }

    private BpmnModelNs.Lane ParseLane(XmlNode node)
    {
        var lane = new BpmnModelNs.Lane
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
        };

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "flowNodeRef" && !string.IsNullOrWhiteSpace(child.InnerText))
                lane.FlowReferences.Add(child.InnerText.Trim());
        }

        return lane;
    }

    private BpmnModelNs.BaseElement? ParseFlowElement(XmlNode node, BpmnModelNs.Process process)
    {
        var converter = BpmnConverterRegistry.GetConverter(node.LocalName);
        if (converter != null)
            return converter.ConvertToBpmnModel(node, process);

        var elementId = GetAttributeValue(node, "id") ?? "<unknown>";
        throw new WorkflowEngineException(
            $"Unsupported BPMN flow element '{node.LocalName}' (id='{elementId}') inside process '{process.Id ?? "<unknown>"}'.");
    }

    private BpmnModelNs.Message ParseMessage(XmlNode node) => new()
    {
        Id = GetAttributeValue(node, "id"),
        Name = GetAttributeValue(node, "name"),
        ItemRef = ParseItemRef(GetAttributeValue(node, "itemRef"), node)
    };

    private BpmnModelNs.Error ParseError(XmlNode node) => new()
    {
        Id = GetAttributeValue(node, "id"),
        Name = GetAttributeValue(node, "name"),
        ErrorCode = GetAttributeValue(node, "errorCode")
    };

    private BpmnModelNs.Signal ParseSignal(XmlNode node) => new()
    {
        Id = GetAttributeValue(node, "id"),
        Name = GetAttributeValue(node, "name"),
        Scope = GetAttributeValue(node, "activiti", "scope")
    };

    private BpmnModelNs.DataStore ParseDataStore(XmlNode node)
    {
        var dataStore = new BpmnModelNs.DataStore
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name"),
            ItemSubjectRef = GetAttributeValue(node, "itemSubjectRef")
        };

        var capacity = GetAttributeValue(node, "capacity");
        if (!string.IsNullOrWhiteSpace(capacity))
        {
            dataStore.Capacity = capacity;
            dataStore.IsUnlimited = false;
        }

        var isUnlimited = GetAttributeValue(node, "isUnlimited");
        if (!string.IsNullOrWhiteSpace(isUnlimited) && bool.TryParse(isUnlimited, out var unlimitedFlag))
            dataStore.IsUnlimited = unlimitedFlag;

        return dataStore;
    }

    private BpmnModelNs.ItemDefinition ParseItemDefinition(XmlNode node) => new()
    {
        Id = GetAttributeValue(node, "id"),
        StructureRef = GetAttributeValue(node, "structureRef"),
        ItemKind = GetAttributeValue(node, "itemKind"),
        IsCollection = bool.TryParse(GetAttributeValue(node, "isCollection"), out var isCollection) && isCollection
    };

    private BpmnModelNs.Import ParseImport(XmlNode node) => new()
    {
        ImportType = GetAttributeValue(node, "importType"),
        Location = GetAttributeValue(node, "location"),
        Namespace = GetAttributeValue(node, "namespace")
    };

    private BpmnModelNs.Resource ParseResource(XmlNode node) => new()
    {
        Id = GetAttributeValue(node, "id"),
        Name = GetAttributeValue(node, "name")
    };

    private BpmnModelNs.Interface ParseInterface(XmlNode node)
    {
        var iface = new BpmnModelNs.Interface
        {
            Id = GetAttributeValue(node, "id"),
            Name = GetAttributeValue(node, "name")
        };

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element || child.LocalName != "operation") continue;

            iface.Operations.Add(new BpmnModelNs.Operation
            {
                Id = GetAttributeValue(child, "id"),
                Name = GetAttributeValue(child, "name"),
                ImplementationRef = GetAttributeValue(child, "implementationRef")
            });
        }

        return iface;
    }

    private void ParseCollaboration(XmlNode collaborationNode, BpmnModelNs.BpmnModel model)
    {
        foreach (XmlNode child in collaborationNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;

            if (child.LocalName == "participant")
            {
                model.Pools.Add(new BpmnModelNs.Pool
                {
                    Id = GetAttributeValue(child, "id"),
                    Name = GetAttributeValue(child, "name"),
                    ProcessRef = GetAttributeValue(child, "processRef")
                });
            }
            else if (child.LocalName == "messageFlow")
            {
                var messageFlow = new BpmnModelNs.MessageFlow
                {
                    Id = GetAttributeValue(child, "id"),
                    Name = GetAttributeValue(child, "name"),
                    SourceRef = GetAttributeValue(child, "sourceRef"),
                    TargetRef = GetAttributeValue(child, "targetRef"),
                    MessageRef = GetAttributeValue(child, "messageRef")
                };

                model.MessageFlows.Add(messageFlow);
                if (!string.IsNullOrEmpty(messageFlow.Id))
                    model.MessageFlowMap[messageFlow.Id] = messageFlow;
            }
        }
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

    private static string? ParseItemRef(string? itemRef, XmlNode node)
    {
        if (string.IsNullOrEmpty(itemRef)) return null;

        var prefixIndex = itemRef.IndexOf(':');
        if (prefixIndex >= 0)
        {
            var prefix = itemRef[..prefixIndex];
            var localName = itemRef[(prefixIndex + 1)..];
            var namespaceUri = node.GetNamespaceOfPrefix(prefix);
            return string.IsNullOrEmpty(namespaceUri) ? itemRef : $"{namespaceUri}:{localName}";
        }

        var definitions = node.OwnerDocument?.DocumentElement;
        var targetNamespace = definitions != null ? GetAttributeValue(definitions, "targetNamespace") : null;
        targetNamespace ??= BpmnConstants.TargetNamespace;
        return $"{targetNamespace}:{itemRef}";
    }
}

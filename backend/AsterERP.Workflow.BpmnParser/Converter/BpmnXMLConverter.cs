using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class BpmnXMLConverter
{
    private static readonly Dictionary<string, BaseBpmnXMLConverter> ConvertersToBpmnMap = new();
    private static readonly Dictionary<Type, BaseBpmnXMLConverter> ConvertersToXMLMap = new();

    private readonly DefinitionsParser _definitionsParser = new();
    private readonly DocumentationParser _documentationParser = new();
    private readonly ExtensionElementsParser _extensionElementsParser = new();
    private readonly ImportParser _importParser = new();
    private readonly InterfaceParser _interfaceParser = new();
    private readonly ItemDefinitionParser _itemDefinitionParser = new();
    private readonly IOSpecificationParser _ioSpecificationParser = new();
    private readonly DataStoreParser _dataStoreParser = new();
    private readonly ErrorParser _errorParser = new();
    private readonly LaneParser _laneParser = new();
    private readonly MessageParser _messageParser = new();
    private readonly MessageFlowParser _messageFlowParser = new();
    private readonly MultiInstanceParser _multiInstanceParser = new();
    private readonly ParticipantParser _participantParser = new();
    private readonly PotentialStarterParser _potentialStarterParser = new();
    private readonly ProcessParser _processParser = new();
    private readonly ResourceParser _resourceParser = new();
    private readonly SignalParser _signalParser = new();
    private readonly SubProcessParser _subProcessParser = new();
    private readonly BpmnShapeParser _bpmnShapeParser = new();
    private readonly BpmnEdgeParser _bpmnEdgeParser = new();

    static BpmnXMLConverter()
    {
        AddConverter(new StartEventXMLConverter());
        AddConverter(new EndEventXMLConverter());
        AddConverter(new UserTaskXMLConverter());
        AddConverter(new ServiceTaskXMLConverter());
        AddConverter(new ScriptTaskXMLConverter());
        AddConverter(new ReceiveTaskXMLConverter());
        AddConverter(new SendTaskXMLConverter());
        AddConverter(new ManualTaskXMLConverter());
        AddConverter(new BusinessRuleTaskXMLConverter());
        AddConverter(new TaskXMLConverter());
        AddConverter(new CallActivityXMLConverter());
        AddConverter(new EventGatewayXMLConverter());
        AddConverter(new ExclusiveGatewayXMLConverter());
        AddConverter(new InclusiveGatewayXMLConverter());
        AddConverter(new ParallelGatewayXMLConverter());
        AddConverter(new ComplexGatewayXMLConverter());
        AddConverter(new SequenceFlowXMLConverter());
        AddConverter(new CatchEventXMLConverter());
        AddConverter(new ThrowEventXMLConverter());
        AddConverter(new BoundaryEventXMLConverter());
        AddConverter(new TextAnnotationXMLConverter());
        AddConverter(new AssociationXMLConverter());
        AddConverter(new DataStoreReferenceXMLConverter());
        AddConverter(new ValuedDataObjectXMLConverter());
        AddConverter(new TimerEventDefinitionXMLConverter());
        AddConverter(new SignalEventDefinitionXMLConverter());
        AddConverter(new MessageEventDefinitionXMLConverter());
        AddConverter(new ErrorEventDefinitionXMLConverter());
        AddConverter(new CancelEventDefinitionXMLConverter());
        AddConverter(new CompensateEventDefinitionXMLConverter());
        AddConverter(new TerminateEventDefinitionXMLConverter());
        AddConverter(new LinkEventDefinitionXMLConverter());
        AddConverter(new EscalationEventDefinitionXMLConverter());
        AddConverter(new ConditionalEventDefinitionXMLConverter());
        AddConverter(new SubprocessXMLConverter());
    }

    private static void AddConverter(BaseBpmnXMLConverter converter)
    {
        ConvertersToBpmnMap[converter.GetXMLElementName()] = converter;
        ConvertersToXMLMap[converter.GetBpmnElementType()] = converter;
    }

    public BpmnModelNs.BpmnModel ConvertToBpmnModel(byte[] xml) => ConvertToBpmnModel(Encoding.UTF8.GetString(xml));

    public BpmnModelNs.BpmnModel ConvertToBpmnModel(string xmlContent)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlContent);

        var model = new BpmnModelNs.BpmnModel();
        var definitions = xmlDoc.DocumentElement!;

        _definitionsParser.Parse(definitions, model);

        BpmnModelNs.Process? activeProcess = null;
        var activeSubProcessList = new List<BpmnModelNs.SubProcess>();
        var parsedLanes = new List<(BpmnModelNs.Lane Lane, BpmnModelNs.Process Process)>();

        foreach (XmlNode node in definitions.ChildNodes)
        {
            if (node.NodeType != XmlNodeType.Element) continue;

            switch (node.LocalName)
            {
                case "resource": _resourceParser.Parse(node, model); break;
                case "signal": _signalParser.Parse(node, model); break;
                case "message": _messageParser.Parse(node, model); break;
                case "error": _errorParser.Parse(node, model); break;
                case "escalation": new EscalationParser().Parse(node, model); break;
                case "import": _importParser.Parse(node, model); break;
                case "itemDefinition": _itemDefinitionParser.Parse(node, model); break;
                case "dataStore": _dataStoreParser.Parse(node, model); break;
                case "interface": _interfaceParser.Parse(node, model); break;
                case "collaboration":
                    ParseCollaboration(node, model);
                    break;
                case "ioSpecification":
                    _ioSpecificationParser.ParseChildElement(node, activeProcess ?? new BpmnModelNs.Process(), model);
                    break;
                case "participant": _participantParser.Parse(node, model); break;
                case "messageFlow": _messageFlowParser.Parse(node, model); break;
                case "process":
                    var process = _processParser.Parse(node, model);
                    if (process != null) { activeProcess = process; model.AddProcess(process); }
                    break;
                case "potentialStarter":
                    if (activeProcess != null) _potentialStarterParser.Parse(node, activeProcess);
                    else
                        throw new WorkflowEngineException(
                            $"Element 'potentialStarter' must be declared inside a process (resourceRef='{GetAttributeValue(node, "resourceRef") ?? "<unknown>"}').");
                    break;
                case "lane":
                    if (activeProcess != null)
                    {
                        var lane = _laneParser.Parse(node, activeProcess, model);
                        parsedLanes.Add((lane, activeProcess));
                    }
                    else
                        throw new WorkflowEngineException(
                            $"Element 'lane' must be declared inside a process (id='{GetAttributeValue(node, "id") ?? "<unknown>"}').");
                    break;
                case "documentation":
                    BpmnModelNs.BaseElement? parentElement = null;
                    if (activeSubProcessList.Count > 0) parentElement = activeSubProcessList[^1];
                    else if (activeProcess != null) parentElement = activeProcess;
                    if (parentElement != null) _documentationParser.ParseChildElement(node, parentElement, model);
                    break;
                case "extensionElements":
                    if (activeProcess != null) _extensionElementsParser.Parse(node, activeSubProcessList, activeProcess, model);
                    break;
                case "subProcess":
                case "transaction":
                case "adHocSubProcess":
                case "eventSubProcess":
                    if (activeProcess != null) _subProcessParser.Parse(node, activeSubProcessList, activeProcess);
                    break;
                case "BPMNShape": _bpmnShapeParser.Parse(node, model); break;
                case "BPMNEdge": _bpmnEdgeParser.Parse(node, model); break;
                default:
                    if (ConvertersToBpmnMap.TryGetValue(node.LocalName, out var converter))
                    {
                        if (activeProcess == null)
                            throw new WorkflowEngineException($"Flow element '{node.LocalName}' must be declared inside a process.");
                        converter.ConvertToBpmnModel(node, model, activeProcess, activeSubProcessList);
                    }
                    else if (string.Equals(node.NamespaceURI, BpmnXMLConstants.BPMN2_NAMESPACE, StringComparison.Ordinal))
                    {
                        throw new WorkflowEngineException($"Unsupported BPMN element '{node.LocalName}' at definitions level.");
                    }
                    break;
            }
        }

        foreach (var process in model.Processes)
            ProcessFlowElements(process.FlowElements, process);

        ValidateLaneFlowReferences(parsedLanes);
        PersistLaneDefinitions(parsedLanes);
        ValidatePoolProcessReferences(model);
        ValidateMessageFlows(model);

        return model;
    }

    public byte[] ConvertToXML(BpmnModelNs.BpmnModel model) => ConvertToXML(model, "UTF-8");

    public byte[] ConvertToXML(BpmnModelNs.BpmnModel model, string encoding)
    {
        using var outputStream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.GetEncoding(encoding),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        using var baseWriter = XmlWriter.Create(outputStream, settings);
        using var xtw = new IndentingXMLStreamWriter(baseWriter);

        DefinitionsRootExport.WriteRootElement(model, xtw, encoding);
        DefinitionsSupplementExport.WriteDefinitionsSupplement(model, xtw);
        CollaborationExport.WritePools(model, xtw);
        DataStoreExport.WriteDataStores(model, xtw);
        SignalAndMessageDefinitionExport.WriteSignalsAndMessages(model, xtw);

        foreach (var process in model.Processes)
        {
            if (process.FlowElements.Count == 0) continue;
            ProcessExport.WriteProcess(process, xtw);
            foreach (var flowElement in process.FlowElements)
                CreateXML(flowElement, model, xtw);
            xtw.WriteEndElement();
        }

        ErrorExport.WriteError(model, xtw);
        BPMNDIExport.WriteBPMNDI(model, xtw);

        xtw.WriteEndElement();
        xtw.WriteEndDocument();
        xtw.Flush();

        return outputStream.ToArray();
    }

    protected void CreateXML(BpmnModelNs.FlowElement flowElement, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        if (flowElement is BpmnModelNs.SubProcess subProcess)
        {
            if (flowElement is BpmnModelNs.Transaction)
                xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_TRANSACTION);
            else if (flowElement is BpmnModelNs.AdhocSubProcess)
                xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_ADHOC_SUBPROCESS);
            else
                xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_SUBPROCESS, BpmnXMLConstants.BPMN2_NAMESPACE);

            xtw.WriteAttributeString("id", subProcess.Id ?? string.Empty);
            xtw.WriteAttributeString("name", !string.IsNullOrEmpty(subProcess.Name) ? subProcess.Name : "subProcess");

            if (subProcess.TriggeredByEvent)
                xtw.WriteAttributeString("triggeredByEvent", "true");

            if (!string.IsNullOrEmpty(subProcess.Documentation))
            {
                xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_DOCUMENTATION, BpmnXMLConstants.BPMN2_NAMESPACE);
                xtw.WriteString(subProcess.Documentation);
                xtw.WriteEndElement();
            }

            bool didWriteExtensionStartElement = WorkflowExtensionListenerExport.WriteListeners(subProcess, false, xtw);
            didWriteExtensionStartElement = BpmnXMLUtil.WriteExtensionElements(subProcess, didWriteExtensionStartElement, xtw);
            if (didWriteExtensionStartElement) xtw.WriteEndElement();

            MultiInstanceExport.WriteMultiInstance(subProcess, xtw);

            foreach (var subElement in subProcess.FlowElements)
                CreateXML(subElement, model, xtw);

            xtw.WriteEndElement();
        }
        else
        {
            if (ConvertersToXMLMap.TryGetValue(flowElement.GetType(), out var converter))
                converter.ConvertToXML(xtw, flowElement, model);
            else
                throw new WorkflowEngineException($"Unsupported BPMN model element '{flowElement.GetType().Name}' for XML export.");
        }
    }

    private void ProcessFlowElements(List<BpmnModelNs.FlowElement> flowElementList, BpmnModelNs.BaseElement parentScope)
    {
        foreach (var flowElement in flowElementList)
        {
            if (flowElement is BpmnModelNs.SequenceFlow sequenceFlow)
            {
                if (string.IsNullOrEmpty(sequenceFlow.SourceRef))
                {
                    throw new WorkflowEngineException(
                        $"SequenceFlow '{sequenceFlow.Id ?? "<unknown>"}' requires non-empty sourceRef.");
                }

                if (!string.IsNullOrEmpty(sequenceFlow.SourceRef))
                {
                    var sourceNode = GetFlowNodeFromScope(sequenceFlow.SourceRef, parentScope);
                    if (sourceNode != null)
                    {
                        sourceNode.OutgoingFlows.Add(sequenceFlow);
                        sequenceFlow.SourceFlowElement = sourceNode;
                    }
                    else
                    {
                        throw new WorkflowEngineException(
                            $"SequenceFlow '{sequenceFlow.Id ?? "<unknown>"}' references missing sourceRef '{sequenceFlow.SourceRef}'.");
                    }
                }

                if (string.IsNullOrEmpty(sequenceFlow.TargetRef))
                {
                    throw new WorkflowEngineException(
                        $"SequenceFlow '{sequenceFlow.Id ?? "<unknown>"}' requires non-empty targetRef.");
                }

                if (!string.IsNullOrEmpty(sequenceFlow.TargetRef))
                {
                    var targetNode = GetFlowNodeFromScope(sequenceFlow.TargetRef, parentScope);
                    if (targetNode != null)
                    {
                        targetNode.IncomingFlows.Add(sequenceFlow);
                        sequenceFlow.TargetFlowElement = targetNode;
                    }
                    else
                    {
                        throw new WorkflowEngineException(
                            $"SequenceFlow '{sequenceFlow.Id ?? "<unknown>"}' references missing targetRef '{sequenceFlow.TargetRef}'.");
                    }
                }
            }
            else if (flowElement is BpmnModelNs.BoundaryEvent boundaryEvent)
            {
                var attachedToElement = GetFlowNodeFromScope(boundaryEvent.AttachedToRefId, parentScope);
                if (attachedToElement is BpmnModelNs.Activity attachedActivity)
                {
                    boundaryEvent.AttachedToRef = attachedActivity;
                    attachedActivity.BoundaryEvents.Add(boundaryEvent);
                }
                else
                {
                    throw new WorkflowEngineException(
                        $"BoundaryEvent '{boundaryEvent.Id ?? "<unknown>"}' references missing or non-activity attachedToRef '{boundaryEvent.AttachedToRefId ?? "<null>"}'.");
                }
            }
            else if (flowElement is BpmnModelNs.SubProcess subProcess)
            {
                ProcessFlowElements(subProcess.FlowElements, subProcess);
            }

            if (flowElement is BpmnModelNs.ExclusiveGateway exclusiveGateway)
            {
                ValidateGatewayDefaultFlow(exclusiveGateway, parentScope);
            }
            else if (flowElement is BpmnModelNs.InclusiveGateway inclusiveGateway)
            {
                ValidateGatewayDefaultFlow(inclusiveGateway, parentScope);
            }
            else if (flowElement is BpmnModelNs.ComplexGateway complexGateway)
            {
                ValidateGatewayDefaultFlow(complexGateway, parentScope);
            }
            else if (flowElement is BpmnModelNs.EventGateway eventGateway)
            {
                ValidateEventGatewayOutgoingTargets(eventGateway);
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

    private BpmnModelNs.FlowNode? GetFlowNodeFromScope(string? elementId, BpmnModelNs.BaseElement scope)
    {
        if (string.IsNullOrEmpty(elementId)) return null;
        if (scope is BpmnModelNs.Process process)
            return process.FlowElements.Find(e => e.Id == elementId) as BpmnModelNs.FlowNode;
        if (scope is BpmnModelNs.SubProcess subProcess)
            return subProcess.FlowElements.Find(e => e.Id == elementId) as BpmnModelNs.FlowNode;
        return null;
    }

    private static string? GetAttributeValue(XmlNode node, string localName) => node.Attributes?[localName]?.Value;

    private void ParseCollaboration(XmlNode collaborationNode, BpmnModelNs.BpmnModel model)
    {
        foreach (XmlNode child in collaborationNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element)
                continue;

            if (child.LocalName == BpmnXMLConstants.ELEMENT_PARTICIPANT)
            {
                _participantParser.Parse(child, model);
            }
            else if (child.LocalName == BpmnXMLConstants.ELEMENT_MESSAGE_FLOW)
            {
                _messageFlowParser.Parse(child, model);
            }
        }
    }

    private static void ValidateMessageFlows(BpmnModelNs.BpmnModel model)
    {
        foreach (var messageFlow in model.MessageFlows)
        {
            if (string.IsNullOrEmpty(messageFlow.SourceRef) || !PoolExists(model, messageFlow.SourceRef))
            {
                throw new WorkflowEngineException(
                    $"MessageFlow '{messageFlow.Id ?? "<unknown>"}' references missing sourceRef '{messageFlow.SourceRef ?? "<null>"}'.");
            }

            if (string.IsNullOrEmpty(messageFlow.TargetRef) || !PoolExists(model, messageFlow.TargetRef))
            {
                throw new WorkflowEngineException(
                    $"MessageFlow '{messageFlow.Id ?? "<unknown>"}' references missing targetRef '{messageFlow.TargetRef ?? "<null>"}'.");
            }

            if (string.IsNullOrEmpty(messageFlow.MessageRef))
            {
                throw new WorkflowEngineException(
                    $"MessageFlow '{messageFlow.Id ?? "<unknown>"}' requires non-empty messageRef.");
            }

            if (!MessageExists(model, messageFlow.MessageRef))
            {
                throw new WorkflowEngineException(
                    $"MessageFlow '{messageFlow.Id ?? "<unknown>"}' references missing messageRef '{messageFlow.MessageRef}'.");
            }
        }
    }

    private static bool PoolExists(BpmnModelNs.BpmnModel model, string poolId)
    {
        foreach (var pool in model.Pools)
        {
            if (string.Equals(pool.Id, poolId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MessageExists(BpmnModelNs.BpmnModel model, string messageId)
    {
        return model.MessageMap.ContainsKey(messageId);
    }

    private static void ValidatePoolProcessReferences(BpmnModelNs.BpmnModel model)
    {
        foreach (var pool in model.Pools)
        {
            if (string.IsNullOrEmpty(pool.ProcessRef))
            {
                continue;
            }

            var exists = false;
            foreach (var process in model.Processes)
            {
                if (string.Equals(process.Id, pool.ProcessRef, StringComparison.Ordinal))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                throw new WorkflowEngineException(
                    $"Participant '{pool.Id ?? "<unknown>"}' references missing processRef '{pool.ProcessRef}'.");
            }
        }
    }

    private static void ValidateLaneFlowReferences(List<(BpmnModelNs.Lane Lane, BpmnModelNs.Process Process)> parsedLanes)
    {
        foreach (var entry in parsedLanes)
        {
            foreach (var flowRef in entry.Lane.FlowReferences)
            {
                if (string.IsNullOrEmpty(flowRef))
                {
                    continue;
                }

                if (!FlowElementExists(entry.Process.FlowElements, flowRef))
                {
                    throw new WorkflowEngineException(
                        $"Lane '{entry.Lane.Id ?? "<unknown>"}' references missing flowNodeRef '{flowRef}'.");
                }
            }
        }
    }

    private static void PersistLaneDefinitions(List<(BpmnModelNs.Lane Lane, BpmnModelNs.Process Process)> parsedLanes)
    {
        if (parsedLanes.Count == 0)
            return;

        foreach (var processGroup in parsedLanes.GroupBy(p => p.Process))
        {
            if (processGroup.Key == null)
                continue;

            var laneSetElement = new BpmnModelNs.ExtensionElement
            {
                Name = BpmnXMLConstants.ELEMENT_LANESET,
                Namespace = BpmnXMLConstants.BPMN2_NAMESPACE,
                NamespacePrefix = BpmnXMLConstants.BPMN2_PREFIX
            };

            foreach (var laneEntry in processGroup)
            {
                var lane = laneEntry.Lane;
                var laneElement = new BpmnModelNs.ExtensionElement
                {
                    Name = BpmnXMLConstants.ELEMENT_LANE,
                    Namespace = BpmnXMLConstants.BPMN2_NAMESPACE,
                    NamespacePrefix = BpmnXMLConstants.BPMN2_PREFIX
                };

                if (!string.IsNullOrWhiteSpace(lane.Id))
                {
                    laneElement.Attributes.Add(new BpmnModelNs.ExtensionAttribute
                    {
                        Name = BpmnXMLConstants.ATTRIBUTE_ID,
                        Value = lane.Id
                    });
                }

                if (!string.IsNullOrWhiteSpace(lane.Name))
                {
                    laneElement.Attributes.Add(new BpmnModelNs.ExtensionAttribute
                    {
                        Name = BpmnXMLConstants.ATTRIBUTE_NAME,
                        Value = lane.Name
                    });
                }

                foreach (var flowRef in lane.FlowReferences.Where(r => !string.IsNullOrWhiteSpace(r)))
                {
                    laneElement.ChildElements.Add(new BpmnModelNs.ExtensionElement
                    {
                        Name = BpmnXMLConstants.ELEMENT_FLOWNODE_REF,
                        Namespace = BpmnXMLConstants.BPMN2_NAMESPACE,
                        NamespacePrefix = BpmnXMLConstants.BPMN2_PREFIX,
                        ElementText = flowRef
                    });
                }

                laneSetElement.ChildElements.Add(laneElement);
            }

            processGroup.Key.AddExtensionElement(laneSetElement);
        }
    }

    private static bool FlowElementExists(List<BpmnModelNs.FlowElement> flowElements, string flowElementId)
    {
        foreach (var flowElement in flowElements)
        {
            if (string.Equals(flowElement.Id, flowElementId, StringComparison.Ordinal))
            {
                return true;
            }

            if (flowElement is BpmnModelNs.SubProcess subProcess &&
                FlowElementExists(subProcess.FlowElements, flowElementId))
            {
                return true;
            }
        }

        return false;
    }

    private void ValidateGatewayDefaultFlow(BpmnModelNs.Gateway gateway, BpmnModelNs.BaseElement parentScope)
    {
        var defaultFlowId = gateway switch
        {
            BpmnModelNs.ExclusiveGateway e => e.DefaultFlow,
            BpmnModelNs.InclusiveGateway i => i.DefaultFlow,
            BpmnModelNs.ComplexGateway c => c.DefaultFlow,
            _ => null
        };

        if (string.IsNullOrEmpty(defaultFlowId))
        {
            return;
        }

        var defaultFlowElement = GetFlowElementFromScope(defaultFlowId, parentScope);
        if (defaultFlowElement is not BpmnModelNs.SequenceFlow)
        {
            throw new WorkflowEngineException(
                $"Gateway '{gateway.Id ?? "<unknown>"}' references missing default flow '{defaultFlowId}'.");
        }
    }

    private BpmnModelNs.FlowElement? GetFlowElementFromScope(string elementId, BpmnModelNs.BaseElement scope)
    {
        if (scope is BpmnModelNs.Process process)
        {
            return process.FlowElements.Find(e => e.Id == elementId);
        }

        if (scope is BpmnModelNs.SubProcess subProcess)
        {
            return subProcess.FlowElements.Find(e => e.Id == elementId);
        }

        return null;
    }
}


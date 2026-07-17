using System;
using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public class BpmnModel
{
    public Dictionary<string, List<ExtensionAttribute>> DefinitionsAttributes { get; set; } = new();
    public List<Process> Processes { get; set; } = new();
    public List<Pool> Pools { get; set; } = new();
    public Dictionary<string, GraphicInfo> LocationMap { get; set; } = new();
    public Dictionary<string, GraphicInfo> LabelLocationMap { get; set; } = new();
    public Dictionary<string, List<GraphicInfo>> FlowLocationMap { get; set; } = new();
    public Dictionary<string, FlowElement> FlowElementMap { get; private set; } = new();
    public Dictionary<string, MessageFlow> MessageFlowMap { get; set; } = new();
    public Dictionary<string, Message> MessageMap { get; set; } = new();
    public List<MessageFlow> MessageFlows { get; set; } = new();
    public Dictionary<string, Error> ErrorMap { get; set; } = new();
    public Dictionary<string, Escalation> EscalationMap { get; set; } = new();
    public Dictionary<string, ItemDefinition> ItemDefinitionMap { get; set; } = new();
    public Dictionary<string, DataStore> DataStoreMap { get; set; } = new();
    public List<Import> Imports { get; set; } = new();
    public List<Interface> Interfaces { get; set; } = new();
    public List<Artifact> GlobalArtifacts { get; set; } = new();
    public List<Resource> Resources { get; set; } = new();
    public Dictionary<string, string> NamespaceMap { get; set; } = new();
    public List<Signal> Signals { get; set; } = new();
    public Dictionary<string, SequenceFlow> SequenceFlowMap { get; private set; } = new();
    public string? TargetNamespace { get; set; }
    public string? SourceSystemId { get; set; }
    public List<string>? UserTaskFormTypes { get; set; }
    public List<string>? StartEventFormTypes { get; set; }
    public int NextFlowIdCounter { get; set; } = 1;
    public object? EventSupport { get; set; }

    public void AddProcess(Process process)
    {
        Processes.Add(process);
        process.BpmnModel = this;
        BuildFlowElementMap(process);
    }

    public Process? GetProcessById(string processId)
    {
        return Processes.Find(p => p.Id == processId);
    }

    public FlowElement? GetFlowElement(string processId, string flowElementId)
    {
        return FlowElementMap.TryGetValue($"{processId}.{flowElementId}", out var element)
            ? element
            : null;
    }

    public SequenceFlow? GetSequenceFlow(string sequenceFlowId)
    {
        return SequenceFlowMap.GetValueOrDefault(sequenceFlowId);
    }

    private void BuildFlowElementMap(Process process)
    {
        foreach (var flowElement in process.FlowElements)
        {
            FlowElementMap[$"{process.Id}.{flowElement.Id}"] = flowElement;
            if (flowElement is SequenceFlow sf && sf.Id != null)
                SequenceFlowMap[sf.Id] = sf;

            if (flowElement is SubProcess sp)
                BuildFlowElementMapForContainer(sp, process);
        }
    }

    private void BuildFlowElementMapForContainer(SubProcess sp, Process process)
    {
        foreach (var flowElement in sp.FlowElements)
        {
            FlowElementMap[$"{process.Id}.{flowElement.Id}"] = flowElement;
            if (flowElement is SequenceFlow sf && sf.Id != null)
                SequenceFlowMap[sf.Id] = sf;

            if (flowElement is SubProcess innerSp)
                BuildFlowElementMapForContainer(innerSp, process);
        }
    }
}

public class Process : FlowElementsContainerImplementation
{
    public BpmnModel? BpmnModel { get; set; }
    public string? Name { get; set; }
    public bool IsExecutable { get; set; } = true;
    public string? Documentation { get; set; }
    public List<WorkflowExtensionListener> ExecutionListeners { get; set; } = new();
    public List<DataObject> DataObjects { get; set; } = new();
    public List<ValuedDataObject> ValuedDataObjects { get; set; } = new();
    public List<DataStoreReference> DataStoreReferences { get; set; } = new();
    public List<Artifact> Artifacts { get; set; } = new();
    public List<Error> Errors { get; set; } = new();
    public List<Signal> Signals { get; set; } = new();
    public string? CandidateStarterUsers { get; set; }
    public string? CandidateStarterGroups { get; set; }
    public string? VersionTag { get; set; }
    public string? HistoryTimeToLive { get; set; }

    public string? GetInitialFlowElementId()
    {
        return FlowElements.Find(fe => fe is StartEvent)?.Id;
    }

    public FlowElement? GetInitialFlowElement()
    {
        return FlowElements.Find(fe => fe is StartEvent);
    }

    public override BaseElement Clone()
    {
        var clone = new Process
        {
            Id = Id,
            Name = Name,
            IsExecutable = IsExecutable,
            Documentation = Documentation,
            CandidateStarterUsers = CandidateStarterUsers,
            CandidateStarterGroups = CandidateStarterGroups,
            VersionTag = VersionTag,
            HistoryTimeToLive = HistoryTimeToLive
        };
        clone.FlowElements.AddRange(FlowElements.Select(fe => (FlowElement)fe.Clone()));
        clone.ValuedDataObjects.AddRange(ValuedDataObjects.Select(dataObject => (ValuedDataObject)dataObject.Clone()));
        clone.DataStoreReferences.AddRange(DataStoreReferences.Select(dataStoreReference => (DataStoreReference)dataStoreReference.Clone()));
        clone.Artifacts.AddRange(Artifacts.Select(artifact => (Artifact)artifact.Clone()));
        return clone;
    }
}

public class DataObject : BaseElement
{
    public string? Name { get; set; }
    public string? ItemSubjectRef { get; set; }

    public override BaseElement Clone()
    {
        return new DataObject { Id = Id, Name = Name, ItemSubjectRef = ItemSubjectRef };
    }
}

public class Pool : BaseElement
{
    public string? Name { get; set; }
    public string? ProcessRef { get; set; }

    public override BaseElement Clone()
    {
        return new Pool { Id = Id, Name = Name, ProcessRef = ProcessRef };
    }
}

public class Lane : BaseElement
{
    public string? Name { get; set; }
    public Pool? ParentPool { get; set; }
    public List<string> FlowReferences { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new Lane
        {
            Id = Id,
            Name = Name,
            ParentPool = ParentPool
        };
        clone.FlowReferences.AddRange(FlowReferences);
        return clone;
    }
}

using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.DynamicBpmn;

public class DynamicProcessDefinitionBuilder
{
    private readonly BpmnModelNs.BpmnModel _model;
    private readonly BpmnModelNs.Process _process;
    private readonly List<BpmnModelNs.FlowElement> _target;

    public DynamicProcessDefinitionBuilder(string processId, string? processName = null)
    {
        _model = new BpmnModelNs.BpmnModel();
        _process = new BpmnModelNs.Process { Id = processId, Name = processName, IsExecutable = true };
        _model.AddProcess(_process);
        _target = _process.FlowElements;
    }

    private DynamicProcessDefinitionBuilder(BpmnModelNs.BpmnModel model, BpmnModelNs.Process process, List<BpmnModelNs.FlowElement> target)
    {
        _model = model;
        _process = process;
        _target = target;
    }

    public DynamicProcessDefinitionBuilder AddStartEvent(string id, string? name = null)
    {
        _target.Add(new BpmnModelNs.StartEvent { Id = id, Name = name });
        return this;
    }

    public DynamicProcessDefinitionBuilder AddEndEvent(string id, string? name = null)
    {
        _target.Add(new BpmnModelNs.EndEvent { Id = id, Name = name });
        return this;
    }

    public DynamicProcessDefinitionBuilder AddUserTask(string id, string? name = null, string? assignee = null)
    {
        _target.Add(new BpmnModelNs.UserTask { Id = id, Name = name, Assignee = assignee });
        return this;
    }

    public DynamicProcessDefinitionBuilder AddServiceTask(string id, string? name = null, string? implementation = null)
    {
        _target.Add(new BpmnModelNs.ServiceTask { Id = id, Name = name, Implementation = implementation });
        return this;
    }

    public DynamicProcessDefinitionBuilder AddScriptTask(string id, string? name = null, string? script = null, string? scriptFormat = null)
    {
        _target.Add(new BpmnModelNs.ScriptTask { Id = id, Name = name, Script = script, ScriptFormat = scriptFormat });
        return this;
    }

    public DynamicProcessDefinitionBuilder AddExclusiveGateway(string id, string? name = null)
    {
        _target.Add(new BpmnModelNs.ExclusiveGateway { Id = id, Name = name });
        return this;
    }

    public DynamicProcessDefinitionBuilder AddParallelGateway(string id, string? name = null)
    {
        _target.Add(new BpmnModelNs.ParallelGateway { Id = id, Name = name });
        return this;
    }

    public DynamicProcessDefinitionBuilder AddSequenceFlow(string id, string sourceRef, string targetRef, string? name = null, string? condition = null)
    {
        _target.Add(new BpmnModelNs.SequenceFlow
        {
            Id = id,
            Name = name,
            SourceRef = sourceRef,
            TargetRef = targetRef,
            ConditionExpression = condition
        });
        return this;
    }

    public DynamicProcessDefinitionBuilder AddSubProcess(string id, string? name = null, Action<DynamicProcessDefinitionBuilder>? configure = null)
    {
        var subProcess = new BpmnModelNs.SubProcess { Id = id, Name = name };
        _target.Add(subProcess);

        if (configure != null)
        {
            var subBuilder = new DynamicProcessDefinitionBuilder(_model, _process, subProcess.FlowElements);
            configure(subBuilder);
        }

        return this;
    }

    public DynamicProcessDefinitionBuilder AddBoundaryEvent(string id, string attachedToRef, string? name = null)
    {
        _target.Add(new BpmnModelNs.BoundaryEvent
        {
            Id = id,
            Name = name,
            AttachedToRefId = attachedToRef,
            CancelActivity = true
        });
        return this;
    }

    public DynamicProcessDefinitionBuilder AddTimerBoundaryEvent(string id, string attachedToRef, string timerDefinition, bool interrupting = true)
    {
        var boundary = new BpmnModelNs.BoundaryEvent
        {
            Id = id,
            Name = null,
            AttachedToRefId = attachedToRef,
            CancelActivity = interrupting
        };
        boundary.AddAttribute(new BpmnModelNs.ExtensionAttribute
        {
            Namespace = BpmnParser.BpmnConstants.WorkflowExtensionNamespace,
            Name = "timerDefinition",
            Value = timerDefinition,
            NamespacePrefix = "activiti"
        });
        _target.Add(boundary);
        return this;
    }

    public DynamicProcessDefinitionBuilder AddSignalBoundaryEvent(string id, string attachedToRef, string signalRef, bool interrupting = true)
    {
        var boundary = new BpmnModelNs.BoundaryEvent
        {
            Id = id,
            Name = null,
            AttachedToRefId = attachedToRef,
            CancelActivity = interrupting
        };
        boundary.AddAttribute(new BpmnModelNs.ExtensionAttribute
        {
            Namespace = BpmnParser.BpmnConstants.WorkflowExtensionNamespace,
            Name = "signalRef",
            Value = signalRef,
            NamespacePrefix = "activiti"
        });
        _target.Add(boundary);
        return this;
    }

    public DynamicProcessDefinitionBuilder AddMessageBoundaryEvent(string id, string attachedToRef, string messageRef, bool interrupting = true)
    {
        var boundary = new BpmnModelNs.BoundaryEvent
        {
            Id = id,
            Name = null,
            AttachedToRefId = attachedToRef,
            CancelActivity = interrupting
        };
        boundary.AddAttribute(new BpmnModelNs.ExtensionAttribute
        {
            Namespace = BpmnParser.BpmnConstants.WorkflowExtensionNamespace,
            Name = "messageRef",
            Value = messageRef,
            NamespacePrefix = "activiti"
        });
        _target.Add(boundary);
        return this;
    }

    public BpmnModelNs.BpmnModel Build()
    {
        return _model;
    }

    public string BuildXml()
    {
        var exporter = new BpmnParser.BpmnModelExporter();
        return exporter.ExportToXml(_model);
    }
}

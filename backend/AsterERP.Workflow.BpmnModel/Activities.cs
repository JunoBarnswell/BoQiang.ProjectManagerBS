using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public abstract class Activity : FlowNode
{
    public bool IsForCompensation { get; set; }
    public string? DefaultFlow { get; set; }
    public List<string> CandidateStarterUsers { get; set; } = new();
    public List<string> CandidateStarterGroups { get; set; } = new();
    public List<BoundaryEvent> BoundaryEvents { get; set; } = new();
    public MultiInstanceLoopCharacteristics? LoopCharacteristics { get; set; }
}

public abstract class BpmnTask : Activity
{
}

public class UserTask : BpmnTask
{
    public string? Assignee { get; set; }
    public string? Owner { get; set; }
    public List<string> CandidateUsers { get; set; } = new();
    public List<string> CandidateGroups { get; set; } = new();
    public string? FormKey { get; set; }
    public int? Priority { get; set; }
    public string? Category { get; set; }
    public string? SkipExpression { get; set; }
    public List<WorkflowExtensionListener> TaskListeners { get; set; } = new();
    public List<FormProperty> FormProperties { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new UserTask
        {
            Id = Id,
            Name = Name,
            Documentation = Documentation,
            Assignee = Assignee,
            Owner = Owner,
            FormKey = FormKey,
            Priority = Priority,
            Category = Category,
            DefaultFlow = DefaultFlow,
            SkipExpression = SkipExpression
        };
        clone.CandidateUsers = new List<string>(CandidateUsers);
        clone.CandidateGroups = new List<string>(CandidateGroups);
        clone.ExecutionListeners = ExecutionListeners.Select(l => (WorkflowExtensionListener)l.Clone()).ToList();
        clone.FormProperties.AddRange(FormProperties.Select(p => (FormProperty)p.Clone()));
        return clone;
    }
}

public class ServiceTask : BpmnTask
{
    public string? ImplementationType { get; set; }
    public string? Implementation { get; set; }
    public string? ResultVariableName { get; set; }
    public string? Type { get; set; }
    public string? Expression { get; set; }
    public string? DelegateExpression { get; set; }
    public string? Class { get; set; }
    public string? SkippedExpression { get; set; }
    public List<FieldExtension> FieldExtensions { get; set; } = new();
    public string? ExtensionId { get; set; }
    public Dictionary<string, string> ExtensionAttributes { get; set; } = new();
    public List<MapExceptionEntry> MapExceptions { get; set; } = new();
    public bool UseLocalScopeForResult { get; set; }
    public List<IOSpecification> IOSpecification { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new ServiceTask
        {
            Id = Id,
            Name = Name,
            ImplementationType = ImplementationType,
            Implementation = Implementation,
            ResultVariableName = ResultVariableName,
            Type = Type,
            Expression = Expression,
            DelegateExpression = DelegateExpression,
            Class = Class,
            SkippedExpression = SkippedExpression,
            ExtensionId = ExtensionId,
            UseLocalScopeForResult = UseLocalScopeForResult
        };
        clone.FieldExtensions = FieldExtensions.Select(f => (FieldExtension)f.Clone()).ToList();
        clone.ExtensionAttributes = new Dictionary<string, string>(ExtensionAttributes);
        clone.IOSpecification.AddRange(IOSpecification.Select(io => (IOSpecification)io.Clone()));
        return clone;
    }
}

public class ScriptTask : BpmnTask
{
    public string? ScriptFormat { get; set; }
    public string? Script { get; set; }
    public string? ResultVariable { get; set; }
    public bool AutoStoreVariables { get; set; }

    public override BaseElement Clone()
    {
        return new ScriptTask
        {
            Id = Id,
            Name = Name,
            ScriptFormat = ScriptFormat,
            Script = Script,
            ResultVariable = ResultVariable,
            AutoStoreVariables = AutoStoreVariables
        };
    }
}

public class ReceiveTask : BpmnTask
{
    public string? Implementation { get; set; }

    public override BaseElement Clone()
    {
        return new ReceiveTask
        {
            Id = Id,
            Name = Name,
            Implementation = Implementation
        };
    }
}

public class SendTask : BpmnTask
{
    public string? Implementation { get; set; }
    public string? ImplementationType { get; set; }
    public string? Operation { get; set; }
    public string? OperationRef { get; set; }
    public string? Type { get; set; }

    public override BaseElement Clone()
    {
        return new SendTask
        {
            Id = Id,
            Name = Name,
            Implementation = Implementation,
            ImplementationType = ImplementationType,
            Operation = Operation,
            OperationRef = OperationRef,
            Type = Type
        };
    }
}

public class ManualTask : BpmnTask
{
    public override BaseElement Clone()
    {
        return new ManualTask
        {
            Id = Id,
            Name = Name
        };
    }
}

public class BusinessRuleTask : BpmnTask
{
    public string? RuleVariablesInput { get; set; }
    public string? Rules { get; set; }
    public string? ResultVariable { get; set; }
    public bool Exclude { get; set; }

    public override BaseElement Clone()
    {
        return new BusinessRuleTask
        {
            Id = Id,
            Name = Name,
            RuleVariablesInput = RuleVariablesInput,
            Rules = Rules,
            ResultVariable = ResultVariable,
            Exclude = Exclude
        };
    }
}

public class CallActivity : Activity
{
    public string? CalledElement { get; set; }
    public bool CalledElementSameDeployment { get; set; }
    public bool InheritVariables { get; set; }
    public bool InheritBusinessKey { get; set; }
    public string? BusinessKey { get; set; }
    public string? CalledElementType { get; set; }
    public string? ProcessInstanceName { get; set; }
    public bool SameDeployment { get; set; }
    public List<IOSpecification> IOSpecification { get; set; } = new();
    public List<IOParameter> InParameters { get; set; } = new();
    public List<IOParameter> OutParameters { get; set; } = new();
    public List<MapExceptionEntry> MapExceptions { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new CallActivity
        {
            Id = Id,
            Name = Name,
            CalledElement = CalledElement,
            CalledElementSameDeployment = CalledElementSameDeployment,
            InheritVariables = InheritVariables,
            InheritBusinessKey = InheritBusinessKey,
            BusinessKey = BusinessKey,
            CalledElementType = CalledElementType,
            ProcessInstanceName = ProcessInstanceName,
            SameDeployment = SameDeployment
        };
        clone.InParameters.AddRange(InParameters.Select(p => (IOParameter)p.Clone()));
        clone.OutParameters.AddRange(OutParameters.Select(p => (IOParameter)p.Clone()));
        return clone;
    }
}

public class SubProcess : Activity, IFlowElementsContainer
{
    public bool TriggeredByEvent { get; set; }
    public List<FlowElement> FlowElements { get; set; } = new();
    public List<ValuedDataObject> DataObjects { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new SubProcess
        {
            Id = Id,
            Name = Name,
            TriggeredByEvent = TriggeredByEvent,
            DefaultFlow = DefaultFlow
        };
        clone.FlowElements.AddRange(FlowElements.Select(fe => (FlowElement)fe.Clone()));
        return clone;
    }
}

public class Transaction : SubProcess
{
    public override BaseElement Clone()
    {
        var clone = new Transaction
        {
            Id = Id,
            Name = Name,
            TriggeredByEvent = TriggeredByEvent
        };
        clone.FlowElements.AddRange(FlowElements.Select(fe => (FlowElement)fe.Clone()));
        return clone;
    }
}

public class AdhocSubProcess : SubProcess
{
    public string? CompletionCondition { get; set; }
    public string? Ordering { get; set; }
    public string? CancelRemainingInstances { get; set; }

    public bool HasSequentialOrdering => "Sequential".Equals(Ordering, StringComparison.OrdinalIgnoreCase);

    public override BaseElement Clone()
    {
        var clone = new AdhocSubProcess
        {
            Id = Id,
            Name = Name,
            CompletionCondition = CompletionCondition,
            Ordering = Ordering,
            CancelRemainingInstances = CancelRemainingInstances
        };
        clone.FlowElements.AddRange(FlowElements.Select(fe => (FlowElement)fe.Clone()));
        return clone;
    }
}

public class IOSpecification : BaseElement
{
    public List<DataInput> DataInputs { get; set; } = new();
    public List<DataOutput> DataOutputs { get; set; } = new();
    public List<string> DataInputRefs { get; set; } = new();
    public List<string> DataOutputRefs { get; set; } = new();
    public List<InputOutputAssociation> InputOutputAssociations { get; set; } = new();
    public List<IOParameter> InParameters { get; set; } = new();
    public List<IOParameter> OutParameters { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new IOSpecification();
        clone.DataInputs.AddRange(DataInputs.Select(d => (DataInput)d.Clone()));
        clone.DataOutputs.AddRange(DataOutputs.Select(d => (DataOutput)d.Clone()));
        clone.DataInputRefs.AddRange(DataInputRefs);
        clone.DataOutputRefs.AddRange(DataOutputRefs);
        clone.InParameters.AddRange(InParameters.Select(p => (IOParameter)p.Clone()));
        clone.OutParameters.AddRange(OutParameters.Select(p => (IOParameter)p.Clone()));
        return clone;
    }
}

public class DataInput : BaseElement
{
    public string? Name { get; set; }
    public string? ItemSubjectRef { get; set; }

    public override BaseElement Clone()
    {
        return new DataInput { Id = Id, Name = Name, ItemSubjectRef = ItemSubjectRef };
    }
}

public class DataOutput : BaseElement
{
    public string? Name { get; set; }
    public string? ItemSubjectRef { get; set; }

    public override BaseElement Clone()
    {
        return new DataOutput { Id = Id, Name = Name, ItemSubjectRef = ItemSubjectRef };
    }
}

public class InputOutputAssociation : BaseElement
{
    public bool IsInputAssociation { get; set; } = true;
    public string? SourceRef { get; set; }
    public string? TargetRef { get; set; }
    public string? Transformation { get; set; }
    public List<Assignment> Assignments { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new InputOutputAssociation
        {
            Id = Id,
            IsInputAssociation = IsInputAssociation,
            SourceRef = SourceRef,
            TargetRef = TargetRef,
            Transformation = Transformation
        };
        clone.Assignments.AddRange(Assignments.Select(a => (Assignment)a.Clone()));
        return clone;
    }
}

public abstract class ValuedDataObject : BaseElement
{
    public string? Name { get; set; }
    public object? Value { get; set; }
    public string? ItemSubjectRef { get; set; }

    public abstract string TypeName { get; }

    public abstract ValuedDataObject CopyValue();

    public override BaseElement Clone()
    {
        var clone = CopyValue();
        clone.Id = Id;
        clone.Name = Name;
        clone.Value = Value;
        clone.ItemSubjectRef = ItemSubjectRef;
        return clone;
    }
}

public class MultiInstanceLoopCharacteristics : BaseElement
{
    public bool IsSequential { get; set; }
    public string? InputDataItem { get; set; }
    public string? OutputDataItem { get; set; }
    public string? LoopCardinality { get; set; }
    public string? CompletionCondition { get; set; }
    public string? ElementVariable { get; set; }
    public string? CollectionString { get; set; }
    public string? Collection { get; set; }
    public string? CollectionVariable { get; set; }
    public string? ElementIndexVariable { get; set; }

    public override BaseElement Clone()
    {
        return new MultiInstanceLoopCharacteristics
        {
            IsSequential = IsSequential,
            InputDataItem = InputDataItem,
            OutputDataItem = OutputDataItem,
            LoopCardinality = LoopCardinality,
            CompletionCondition = CompletionCondition,
            ElementVariable = ElementVariable,
            CollectionString = CollectionString,
            Collection = Collection,
            CollectionVariable = CollectionVariable,
            ElementIndexVariable = ElementIndexVariable
        };
    }
}

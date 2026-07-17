using System;
using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public interface IFlowElementsContainer
{
    IFlowElementsContainer? ParentContainer { get; set; }
    List<FlowElement> FlowElements { get; }
}

public abstract class FlowElement : BaseElement
{
    public string? Name { get; set; }
    public string? Documentation { get; set; }
    public List<WorkflowExtensionListener> ExecutionListeners { get; set; } = new();
    public IFlowElementsContainer? ParentContainer { get; set; }

    public SubProcess? GetSubProcess()
    {
        return ParentContainer as SubProcess;
    }
}

public class FlowElementsContainerImplementation : BaseElement, IFlowElementsContainer
{
    public List<FlowElement> FlowElements { get; set; } = new();
    public IFlowElementsContainer? ParentContainer { get; set; }

    public override BaseElement Clone()
    {
        var clone = new FlowElementsContainerImplementation
        {
            Id = Id,
            XmlRowNumber = XmlRowNumber,
            XmlColumnNumber = XmlColumnNumber,
            ParentContainer = ParentContainer
        };
        clone.FlowElements.AddRange(FlowElements.Select(fe => (FlowElement)fe.Clone()));
        return clone;
    }
}

public class SequenceFlow : FlowElement
{
    public string? SourceRef { get; set; }
    public string? TargetRef { get; set; }
    public string? ConditionExpression { get; set; }
    public bool IsImmediate { get; set; }
    public FlowNode? TargetFlowElement { get; set; }
    public FlowNode? SourceFlowElement { get; set; }

    public override BaseElement Clone()
    {
        return new SequenceFlow
        {
            Id = Id,
            Name = Name,
            SourceRef = SourceRef,
            TargetRef = TargetRef,
            ConditionExpression = ConditionExpression,
            IsImmediate = IsImmediate,
            TargetFlowElement = TargetFlowElement,
            SourceFlowElement = SourceFlowElement
        };
    }
}

public class Association : BaseElement
{
    public string? SourceRef { get; set; }
    public string? TargetRef { get; set; }
    public string? AssociationDirection { get; set; }

    public override BaseElement Clone()
    {
        return new Association
        {
            Id = Id,
            SourceRef = SourceRef,
            TargetRef = TargetRef,
            AssociationDirection = AssociationDirection
        };
    }
}

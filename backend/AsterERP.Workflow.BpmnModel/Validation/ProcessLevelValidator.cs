using System.Collections.Generic;
using System.Linq;

namespace AsterERP.Workflow.BpmnModel.Validation;

public abstract class ProcessLevelValidator : IProcessValidator
{
    public abstract string ValidatorName { get; }

    public IEnumerable<ValidationError> Validate(BpmnModel model)
    {
        var errors = new List<ValidationError>();
        foreach (var process in model.Processes)
            ExecuteValidation(model, process, errors);
        return errors;
    }

    protected abstract void ExecuteValidation(BpmnModel model, Process process, List<ValidationError> errors);

    protected ValidationError CreateError(string problem, string description, Process process, FlowElement? flowElement = null)
    {
        var error = new ValidationError
        {
            Type = ValidationErrorType.Error,
            Problem = problem,
            Description = description,
            ProcessDefinitionId = process.Id,
            ProcessDefinitionName = process.Name
        };

        if (flowElement != null)
        {
            error.FlowElementId = flowElement.Id;
            error.FlowElementName = flowElement.Name;
            error.XmlRowNumber = flowElement.XmlRowNumber > 0 ? flowElement.XmlRowNumber : null;
            error.XmlColumnNumber = flowElement.XmlColumnNumber > 0 ? flowElement.XmlColumnNumber : null;
        }

        return error;
    }

    protected ValidationError CreateWarning(string problem, string description, Process process, FlowElement? flowElement = null)
    {
        var error = CreateError(problem, description, process, flowElement);
        error.Type = ValidationErrorType.Warning;
        return error;
    }

    protected void AddError(List<ValidationError> errors, string problem, string description, Process process, FlowElement? flowElement = null)
    {
        errors.Add(CreateError(problem, description, process, flowElement));
    }

    protected void AddWarning(List<ValidationError> errors, string problem, string description, Process process, FlowElement? flowElement = null)
    {
        errors.Add(CreateWarning(problem, description, process, flowElement));
    }

    protected List<T> FindFlowElementsOfType<T>(IFlowElementsContainer container) where T : FlowElement
    {
        var result = new List<T>();
        CollectFlowElements(container, result);
        return result;
    }

    private void CollectFlowElements<T>(IFlowElementsContainer container, List<T> result) where T : FlowElement
    {
        foreach (var element in container.FlowElements)
        {
            if (element is T typed)
                result.Add(typed);
            if (element is IFlowElementsContainer nestedContainer)
                CollectFlowElements(nestedContainer, result);
        }
    }

    protected List<T> FindDirectFlowElementsOfType<T>(IFlowElementsContainer container) where T : FlowElement
    {
        var result = new List<T>();
        foreach (var element in container.FlowElements)
        {
            if (element is T typed)
                result.Add(typed);
        }
        return result;
    }

    protected FlowElement? FindFlowElementById(Process process, string id)
    {
        return FindFlowElementByIdInContainer(process, id);
    }

    private FlowElement? FindFlowElementByIdInContainer(IFlowElementsContainer container, string id)
    {
        foreach (var element in container.FlowElements)
        {
            if (element.Id == id)
                return element;
            if (element is IFlowElementsContainer nestedContainer)
            {
                var found = FindFlowElementByIdInContainer(nestedContainer, id);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    protected IFlowElementsContainer? FindContainerForElement(IFlowElementsContainer container, string elementId)
    {
        foreach (var element in container.FlowElements)
        {
            if (element.Id == elementId)
                return container;
            if (element is IFlowElementsContainer nestedContainer)
            {
                var found = FindContainerForElement(nestedContainer, elementId);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    protected List<SequenceFlow> GetIncomingFlows(Process process, string elementId)
    {
        var allSequenceFlows = FindFlowElementsOfType<SequenceFlow>(process);
        return allSequenceFlows.Where(sf => sf.TargetRef == elementId).ToList();
    }

    protected List<SequenceFlow> GetOutgoingFlows(Process process, string elementId)
    {
        var allSequenceFlows = FindFlowElementsOfType<SequenceFlow>(process);
        return allSequenceFlows.Where(sf => sf.SourceRef == elementId).ToList();
    }

    protected List<EventDefinition> GetEventDefinitions(FlowElement flowElement)
    {
        if (flowElement is CatchEvent catchEvent)
            return catchEvent.EventDefinitions;
        if (flowElement is ThrowEvent throwEvent)
            return throwEvent.EventDefinitions;
        return new List<EventDefinition>();
    }
}

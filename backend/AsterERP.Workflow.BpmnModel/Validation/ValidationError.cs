namespace AsterERP.Workflow.BpmnModel.Validation;

public enum ValidationErrorType
{
    Error,
    Warning
}

public class ValidationError
{
    public ValidationErrorType Type { get; set; } = ValidationErrorType.Error;
    public string? Problem { get; set; }
    public string? Description { get; set; }
    public string? ProcessDefinitionId { get; set; }
    public string? ProcessDefinitionName { get; set; }
    public string? FlowElementId { get; set; }
    public string? FlowElementName { get; set; }
    public int? XmlRowNumber { get; set; }
    public int? XmlColumnNumber { get; set; }

    public bool IsError => Type == ValidationErrorType.Error;
    public bool IsWarning => Type == ValidationErrorType.Warning;

    public override string ToString() => $"[{Type}] {Problem}: {Description} (Process={ProcessDefinitionId}, Element={FlowElementId})";
}

namespace AsterERP.Workflow.Processing.Validation;

public sealed class WorkflowProcessingValidationIssue
{
    public string Severity { get; init; } = "Error";

    public string ErrorCode { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? NodeId { get; init; }

    public string? EdgeId { get; init; }

    public string? Field { get; init; }
}

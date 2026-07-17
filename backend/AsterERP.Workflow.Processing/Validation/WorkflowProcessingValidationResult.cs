namespace AsterERP.Workflow.Processing.Validation;

public sealed class WorkflowProcessingValidationResult
{
    public IReadOnlyList<WorkflowProcessingValidationIssue> Issues { get; init; } = [];

    public bool IsValid => Issues.All(issue => !string.Equals(issue.Severity, "Error", StringComparison.OrdinalIgnoreCase));

    public int ErrorCount => Issues.Count(issue => string.Equals(issue.Severity, "Error", StringComparison.OrdinalIgnoreCase));

    public int WarningCount => Issues.Count(issue => string.Equals(issue.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
}

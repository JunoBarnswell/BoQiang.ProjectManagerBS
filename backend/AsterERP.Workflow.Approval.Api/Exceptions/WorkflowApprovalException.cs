namespace AsterERP.Workflow.Approval.Api.Exceptions;

public class WorkflowApprovalException : Exception
{
    public string? Key { get; }
    public object[]? Values { get; }

    public WorkflowApprovalException() { }

    public WorkflowApprovalException(string message, Exception innerException) : base(message, innerException) { }

    public WorkflowApprovalException(string message) : base(message) { }

    public WorkflowApprovalException(string key, string message) : base(message)
    {
        Key = key;
    }

    public WorkflowApprovalException(string key, object value, string message) : base(message)
    {
        Key = key;
        Values = [value];
    }

    public WorkflowApprovalException(string key, object[] values, string message) : base(message)
    {
        Key = key;
        Values = values;
    }
}

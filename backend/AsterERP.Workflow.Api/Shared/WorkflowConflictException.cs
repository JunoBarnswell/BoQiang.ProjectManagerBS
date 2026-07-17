namespace AsterERP.Workflow.Api.Shared;

public class WorkflowConflictException : WorkflowApiException
{
    public WorkflowConflictException(string message) : base(409, message) { }

    public WorkflowConflictException(string message, Exception innerException) : base(409, message, innerException) { }
}

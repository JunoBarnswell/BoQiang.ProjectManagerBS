namespace AsterERP.Workflow.Api.Shared;

public class WorkflowForbiddenException : WorkflowApiException
{
    public WorkflowForbiddenException(string message) : base(403, message) { }

    public WorkflowForbiddenException(string message, Exception innerException) : base(403, message, innerException) { }
}

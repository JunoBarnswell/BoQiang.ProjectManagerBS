namespace AsterERP.Workflow.Api.Shared;

public class WorkflowNotFoundException : WorkflowApiException
{
    public WorkflowNotFoundException(string message) : base(404, message) { }

    public WorkflowNotFoundException(string message, Exception innerException) : base(404, message, innerException) { }
}
